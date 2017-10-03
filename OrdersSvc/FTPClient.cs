using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace OrdersSvc
{
    class FTPClient
    {
        public String UriDownload = Properties.Settings.Default.UriDownload;
        public String UriUpload = Properties.Settings.Default.UriUpload;
        public String User = Properties.Settings.Default.User;
        public String Pwd = Properties.Settings.Default.Pwd;

        public LogEventi ev = new LogEventi();
        public String Source = "FTP-";// sorgente parziale

        public String ServicePathIn = AppDomain.CurrentDomain.BaseDirectory + @"\InBound\";// percorso installazione servizio
        public String ServicePathOut = AppDomain.CurrentDomain.BaseDirectory + @"\OutBound\";// percorso installazione servizio

        public  List<String> EnumerateFile_FTP()
        {
            List<String> ls_files = new List<String> { };

            try
            {
              
                ev.WriteEventToMyLog(Source + "Lista Files", "lista files su FTP", EventLogEntryType.Information, 1);
                // Get the object used to communicate with the server.
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(UriDownload);

                request.Method = WebRequestMethods.Ftp.ListDirectory;// WebRequestMethods.Ftp.ListDirectoryDetails;

                // This example assumes the FTP site uses anonymous logon.
                request.Credentials = new NetworkCredential(User, Pwd);

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                String ListaFiles = String.Empty;
                while (!reader.EndOfStream)
                {
                    String file = reader.ReadLine();

                    if (file.ToLower().Contains(".xml"))
                    {

                        ls_files.Add(file);
                       
                        
                    }
                }

                ev.WriteEventToMyLog(Source + "Files", ListaFiles + "\nStato:" + response.StatusDescription, EventLogEntryType.Information, 2);
                

                reader.Close();
                response.Close();

                return ls_files;

            }
            catch(Exception ex)
            {
                // invio email con errore casomai
                sendAlert("EnumerateFile_FTP<br><hr><br>" + ex.ToString());
                return ls_files;
            }



        }

        public Boolean Download_FTP(String FileName)
        {
            ev.WriteEventToMyLog(Source + "Download", "Download file: " + FileName, EventLogEntryType.Information, 3);
            // Get the object used to communicate with the server.
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(UriDownload + "/" + FileName);

                request.Method = WebRequestMethods.Ftp.DownloadFile;

                // This example assumes the FTP site uses anonymous logon.
                request.Credentials = new NetworkCredential(User, Pwd);

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                String dataFile = reader.ReadToEnd();
                Console.WriteLine(dataFile);
                StreamWriter sw = new StreamWriter(ServicePathIn + FileName);
                sw.Write(dataFile);
                sw.Close();


                ev.WriteEventToMyLog(Source + "Download", "Download file: '" + FileName + "' Completato\n" + response.StatusDescription, EventLogEntryType.Information, 3);


                reader.Close(); 
                response.Close();
            }
            catch (Exception ex)
            {
                sendAlert("Download_FTP<br><hr><br>" + ex.ToString());
                return false;
            }

            return true;
        }

        public   Boolean Upload_FTP(String FileName)
        {
            ev.WriteEventToMyLog(Source + "UPLOAD File", "upload: " + FileName, EventLogEntryType.Information, 8);
            // Get the object used to communicate with the server.
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(UriUpload + "/" + Path.GetFileName(FileName));

                request.Method = WebRequestMethods.Ftp.UploadFile;

                // This example assumes the FTP site uses anonymous logon.
                request.Credentials = new NetworkCredential(User, Pwd);

                // Copy the contents of the file to the request stream.
                StreamReader sourceStream = new StreamReader(FileName);
                byte[] fileContents = Encoding.UTF8.GetBytes(sourceStream.ReadToEnd());
                sourceStream.Close();
                request.ContentLength = fileContents.Length;

                Stream requestStream = request.GetRequestStream();
                requestStream.Write(fileContents, 0, fileContents.Length);
                requestStream.Close(); 

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                ev.WriteEventToMyLog(Source + "UPLOAD File", "upload Complete:\n" + response.StatusDescription, EventLogEntryType.Information, 8);

                response.Close();
            }
            catch (Exception ex)
            {
                sendAlert("Upload_FTP<br><hr><br>" + ex.ToString());
                return false; 
            }

            return true;
        }

        public Boolean Delete_FTP(String FileName)
        {
            // Get the object used to communicate with the server.
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(UriDownload + "/" + FileName);
                request.Credentials = new NetworkCredential(User, Pwd);

                request.Method = WebRequestMethods.Ftp.DeleteFile;

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                ev.WriteEventToMyLog(Source + "Delete FILE", "Cancellato file Orders:'" + FileName + "' da FTP\n" + response.StatusDescription
                    , EventLogEntryType.Warning, 1);

                response.Close();

            }
            catch (Exception ex)
            {
                sendAlert("Delete_FTP<br><hr><br>" + ex.ToString());
                return false;
            }
            return true;

        }







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
