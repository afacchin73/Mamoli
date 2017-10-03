using System;
using System.Collections.Generic;
 
using System.Diagnostics;
using System.IO;
 
using System.ServiceProcess;

using System.Timers;


namespace OrdersSvc
{
    public partial class Service1 : ServiceBase
    {
        public LogEventi ev = new LogEventi();
        public String Source = "Orders-";// sorgente parziale 

        public Timer timerInBound = new Timer();// timer per ricezione ordini ORDERS da FTP 
        public Timer timerOutBound = new Timer();// timer per invio Bolle DESADV a FTP

        public Int32 RefreshInbound = 1000 * 60 * Convert.ToInt32(Properties.Settings.Default.RefreshIn);// refresh timer in
        public Int32 RefreshOutbound = 1000 * 60 * Convert.ToInt32(Properties.Settings.Default.RefreshOut);// refresh timer out

        public String ServicePathIn = AppDomain.CurrentDomain.BaseDirectory + @"\InBound\";// percorso installazione servizio
        public String ServicePathOut = AppDomain.CurrentDomain.BaseDirectory + @"\OutBound\";// percorso installazione servizio
        public String BackupFolder = AppDomain.CurrentDomain.BaseDirectory + @"\OutBound\Backup\";// percorso installazione servizio

        // flag per sapere se ad ogni refresh dei timer posso operare oppure sta ancora girando il precedente
        public Boolean isRunning_Orders = false;
        public Boolean isRunning_OutBound = false;

        public Service1()
        {
            InitializeComponent();
        }


        #region GESTIONE START STOP SERVIZIO
        protected override void OnStart(string[] args)
        {
            ev.WriteEventToMyLog(Source + "Service", "Avvio Servizio Orders", EventLogEntryType.Information, 1);

            // creazione cartelle
            DirectoryInfo dirIn = new DirectoryInfo(ServicePathIn);
            DirectoryInfo dirOut = new DirectoryInfo(ServicePathOut);
            DirectoryInfo dirBck = new DirectoryInfo(BackupFolder);

            if (!dirIn.Exists) dirIn.Create();            
            if (!dirOut.Exists) dirOut.Create();
            if (!dirBck.Exists) dirBck.Create();


            timerInBound.Interval = RefreshInbound;
            timerInBound.Elapsed += TimerInBound_Elapsed;
            timerInBound.Enabled=true;
            timerInBound.Start();
            ev.WriteEventToMyLog(Source + "Timer InBound", "Avvio Timer InBound Orders", EventLogEntryType.Information, 2);

            timerOutBound.Interval = RefreshOutbound;
            timerOutBound.Elapsed += TimerOutBound_Elapsed;
            timerOutBound.Enabled = true;
            timerOutBound.Start();
            ev.WriteEventToMyLog(Source + "Timer OutBound", "Avvio Timer OutBound Bolle", EventLogEntryType.Information, 3);
        }



        protected override void OnStop()
        {
            ev.WriteEventToMyLog(Source + "Service", "Stop Servizio Orders", EventLogEntryType.Warning, 1);
        }

        #endregion


        #region TIMERS
        private void TimerInBound_Elapsed(object sender, ElapsedEventArgs e)
        {

        
            Boolean res = false;

            if (!isRunning_Orders)
            {
                ev.WriteEventToMyLog(Source + "Timer InBound", "Elaborazione autorizzata", EventLogEntryType.Information, 5);
                isRunning_Orders = true;

                res = get_Orders_from_FTP();

                Cleaning_File(ServicePathIn);// pulizia ordini più vecchi di 1 anno

                isRunning_Orders = false;
            }

        }

        private void Cleaning_File(String DirectoryName)
        {
            ev.WriteEventToMyLog(Source + "Cleaning", "Cleaning " + DirectoryName, EventLogEntryType.Information, 10);

            DirectoryInfo directory = new DirectoryInfo(DirectoryName);
            DateTime today = DateTime.Now;
            foreach(FileInfo f in directory.GetFiles("*.xml"))
            {

                if(f.CreationTime < today.AddDays(-365))
                {
                    ev.WriteEventToMyLog(Source + "Cleaning", "Eliminazione file: " + f.Name + "creato in data " + f.CreationTime.ToString(), EventLogEntryType.Information, 6);
                    f.Delete();
                }

            }



        }

        private void TimerOutBound_Elapsed(object sender, ElapsedEventArgs e)
        {
            Boolean res = false;

            if (!isRunning_OutBound)
            {
                ev.WriteEventToMyLog(Source + "Timer OutBound", "Elaborazione autorizzata", EventLogEntryType.Information, 6);
                isRunning_OutBound = true;

                res = put_OutBound_File_to_FTP();

                Cleaning_File(BackupFolder);

                isRunning_OutBound = false;
            }

               
            }

        #endregion

    


        #region FTP MANAGER

        public Boolean get_Orders_from_FTP()
        {
            
            XmlFunction xmlf = new XmlFunction(); // classe xml           
            FTPClient ftp = new FTPClient(); // classe ftp

            try
            {
                List<String> ls_files = ftp.EnumerateFile_FTP(); // creo la lista dei file da scaricare
                foreach (String f in ls_files)  // scarico i files xml trovati e inserisco gli ordini
                {
                    /*------------------------------------------------------*/

                    Boolean res = ftp.Download_FTP(f); // download da FTP          
                    if (res) // se scaricato correttamente inserisco nella tabella ordersp
                    {
                        Boolean isPutInTable = xmlf.Deserialize_ORDERS(ServicePathIn + f);// deserializzo e inserisco e ritorna true o false                    
                        if (isPutInTable) ftp.Delete_FTP(f);// se ho inserito l'ordine in DB2 allora cancello il file dall'ftp
                    }

                    /*-------------------------------------------------------*/

                }

                return true;
            }
            catch (Exception ex)
            {
                sendAlert("get_Orders_from_FTP<br><hr><br>" + ex.ToString());
                ev.WriteEventToMyLog(Source + "get_Orders_from_FTP", "ERRORE\n" + ex.ToString(), EventLogEntryType.Error, 99);
                return false;
            }
        }


        public Boolean put_OutBound_File_to_FTP()
        {
            XmlFunction xmlf = new XmlFunction();
            FTPClient ftp = new FTPClient(); // classe ftp

            try
            {
                try
                {
                    xmlf.read_CONORD0F();// legge  converte in xml e salva in folder Outbond
                }
                catch (Exception ex)
                {

                    ev.WriteEventToMyLog(Source + " Put CONORD0F", "ERRORE\n" + ex.ToString(), EventLogEntryType.Error, 99);
                }
                try
                {
                    xmlf.read_BOLLE00F();// legge  converte in xml e salva in folder Outbond
                }
                catch (Exception ex)
                {

                    ev.WriteEventToMyLog(Source + " Put BOLLE00F", "ERRORE\n" + ex.ToString(), EventLogEntryType.Error, 99);
                }

                DirectoryInfo Outbound = new DirectoryInfo(ServicePathOut);

                foreach (FileInfo f in Outbound.GetFiles("*.xml"))
                { 
                    Boolean isUpladed = ftp.Upload_FTP(f.FullName);

                    if (isUpladed) File.Move(f.FullName, BackupFolder + f.Name);
                    
                }
            }
            catch (Exception ex)
            {
                sendAlert("put_OutBound_File_to_FTP<br><hr><br>" + ex.ToString());
                ev.WriteEventToMyLog(Source + "put_OutBound_File_to_FTP", "ERRORE\n" + ex.ToString(), EventLogEntryType.Error, 99);
                return false;
            }


            return true;
        }
        #endregion


        #region SEND EMAIL ALERT

        public void sendAlert(String bodyText)
        {
            Email e = new Email();
            List<String> to = new List<string> { "andrea.facchin@soluzioniedp.it" };
            e.SendSMTP(
                "smtp.soluzioniedp.it"
                , "25"
                , "support.T04@soluzioniedp.it"
                , "support20110628"
                , "ERRORE - MAMOLI ORDERS EXCHANGE SERVICE"
                , bodyText
                , to
                , "support.T04@soluzioniedp.it"
                , ""
                , false
                , false);

        }

        #endregion


    }
}
