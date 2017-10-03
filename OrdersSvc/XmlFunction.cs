using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OrdersSvc
{
    class XmlFunction
    {
        public char separator = Convert.ToChar(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        public LogEventi ev = new LogEventi();
        public String Source = "XML-";// sorgente parziale xx
        public String ServicePathOut = AppDomain.CurrentDomain.BaseDirectory + @"\OutBound\";// percorso installazione servizio

        #region ORDINE
        public Boolean Deserialize_ORDERS(String FilenameComplete)
        {

            ev.WriteEventToMyLog(Source + "Deserialize Orders", "deserializzo " + FilenameComplete, EventLogEntryType.Information, 1);

            XmlSerializer deserializer = new XmlSerializer(typeof(pAXORDERS));
            TextReader Txtreader = new StreamReader(FilenameComplete);
            object obj = deserializer.Deserialize(Txtreader);
            pAXORDERS ordine = (pAXORDERS)obj;
            

            order_testata ordT = new order_testata();
            order_riga ordR = new order_riga();

            try
            {

                foreach (pAXORDERSPAXDocumentHeader v in ordine.pAXDocumentHeader)
                {


                    foreach (pAXORDERSPAXDocumentHeaderPAXDateTimes dt in v.pAXDateTimes)
                    {
                        string tipoData = dt.DateTimeType;
                        if (tipoData.ToLower() == "desireddate")
                        {
                            ordT.DesiredDate = Convert.ToDateTime(dt.DateAndTime).ToString();
                            break;
                        }
                    }

                    ordT.DocHeaderID = v.DocHeaderID;
                    ordT.DocumentType = v.DocumentType;
                    ordT.DocumentDate = Convert.ToDateTime(v.DocumentDate).ToShortDateString();
                    ordT.DocumentNumber = v.DocumentNumber;



                    foreach (pAXORDERSPAXDocumentHeaderPAXDocumentLine dl in v.pAXDocumentLine)
                    {
                        ordR.DocLineID = dl.DocLineID;
                        ordR.ParentID = dl.ParentID;
                        ordR.PositionNoSender = dl.PositionNoSender.ToString().Replace(",", ".");
                        ordR.PartIDSender = dl.PartIDSender;
                        ordR.EAN = dl.EAN;
                        ordR.PartDesc = dl.PartDesc1;
                        ordR.Quantity = dl.Quantity.ToString();
                        ordR.PositionSequentialNo = dl.PositionSequentialNo.ToString();

                    }

                }

                Txtreader.Close();
                ev.WriteEventToMyLog(Source + "Insert Orders", "Inserisco " + FilenameComplete, EventLogEntryType.Information, 2);
            }
            catch (Exception ex)
            {
                Txtreader.Close();
                ev.WriteEventToMyLog(Source + "Insert Orders", "ERRORE:\nFile: " + FilenameComplete + "\n--------\n"+ ex.ToString(), EventLogEntryType.Error, 92);
                return false;
            }
            Boolean res= Insert_Orders(ordT, ordR);

            return res;


        }

        private Boolean Insert_Orders(order_testata ordT, order_riga ordR)
        {
            OdbcConnection cn = new OdbcConnection();
            OdbcCommand cmd = new OdbcCommand();


            try
            {
                String ConnectionString = Properties.Settings.Default.CnStriSeries;


                cn.ConnectionString = ConnectionString;
                cmd.Connection = cn;
                cmd.Parameters.Clear();

                // TESTATE
                ev.WriteEventToMyLog(Source + "Insert Header", "inserimento testata", EventLogEntryType.Information, 3);

                String queryT = "INSERT INTO ORDERST (DOCID,DOCTYPE,DOCNUMBER,DOCDATE,DESDATE)"
                    + " VALUES(@DOCID,@DOCTYPE,@DOCNUMBER,@DOCDATE,@DESDATE)";

                queryT = queryT.Replace("@DOCID", "'" + ordT.DocHeaderID + "'")
                    .Replace("@DOCTYPE", "'" + ordT.DocumentType + "'")
                    .Replace("@DOCNUMBER", "'" + ordT.DocumentNumber + "'")
                    .Replace("@DESDATE", "'" + ordT.DesiredDate + "'")
                    .Replace("@DOCDATE", "'" + ordT.DocumentDate + "'");


                cmd.CommandText = queryT;
                cn.Open();
                cmd.ExecuteNonQuery();

                // RIGHE
                ev.WriteEventToMyLog(Source + "Insert Row", "inserimento riga", EventLogEntryType.Information, 4);

                String queryR = "INSERT INTO ORDERSR (DOCLINEID,PARENTID,POSSEQNO,POSNOSENDER,PARTIDSENDER,EAN,PARTDESC,QUANTITY)"
                    + " VALUES(@DOCLINEID,@PARENTID,@POSSEQNO,@POSNOSENDER,@PARTIDSENDER,@EAN,@PARTDESC,@QUANTITY)";

                queryR = queryR.Replace("@DOCLINEID", "'" + ordR.DocLineID + "'")
                    .Replace("@PARENTID", "'" + ordR.ParentID + "'")
                    .Replace("@POSSEQNO", "'" + ordR.PositionSequentialNo + "'")
                    .Replace("@POSNOSENDER", "'" + ordR.PositionNoSender + "'")
                    .Replace("@PARTIDSENDER", "'" + ordR.PartIDSender + "'")
                    .Replace("@EAN", "'" + ordR.EAN + "'")
                    .Replace("@PARTDESC", "'" + ordR.PartDesc + "'")
                    .Replace("@QUANTITY", "'" + ordR.Quantity + "'");

                cmd.CommandText = queryR;
                cmd.ExecuteNonQuery();

                cn.Close();
            }
            catch (Exception ex)
            {

                if (cn.State == ConnectionState.Open)
                    cn.Close();

                sendAlert("Insert_Orders<br><hr><br>ERRORE:" + ordT.DocHeaderID.ToString() + "<br>" + ex.ToString());
                ev.WriteEventToMyLog(Source + "Insert Orders", "ERRORE: " + ordT.DocHeaderID.ToString() +"\n"+ex.ToString(), EventLogEntryType.Error, 99);

                return false;
            }

            return true;

        }
        #endregion

        #region CONFERMA ORDINE
        public void read_CONORD0F()
        {
            ev.WriteEventToMyLog(Source + "Out ConOrdine", "Lettura e conversione conferma ordine", EventLogEntryType.Information,7);

            OdbcConnection cn = new OdbcConnection();
            OdbcCommand cmd = new OdbcCommand();

            String ConnectionString = Properties.Settings.Default.CnStriSeries;




            cn.ConnectionString = ConnectionString;
            cmd.Connection = cn;





            String query = @"SELECT * FROM CONORD0F";



            cmd.CommandText = query;
            cn.Open();
            OdbcDataReader rd = cmd.ExecuteReader();

            while (rd.Read())
            {

                CONORD0F row = new CONORD0F
                {
                    prefisso = rd.GetValue(0).ToString(),
                    nOrdine = rd.GetValue(1).ToString(),
                    dtOrdine = rd.GetValue(2).ToString(),
                    rigOrdine = rd.GetValue(3).ToString(),
                    articolo = rd.GetValue(4).ToString(),
                    descrizione = rd.GetValue(5).ToString(),
                    qta = rd.GetValue(6).ToString(),
                    dtConsegna = rd.GetValue(7).ToString(),
                    original_DOCID = rd.GetValue(8).ToString(),
                    original_DOCNR = rd.GetValue(9).ToString(),
                    original_DOCDATA = rd.GetValue(10).ToString(),
                    original_RIGID = rd.GetValue(11).ToString(),
                    original_EAN = rd.GetValue(12).ToString(),
                    original_POSSEQNBR = rd.GetValue(13).ToString(),
                    original_POSNOSENDER = rd.GetValue(14).ToString().Replace(".", separator.ToString()).Replace(",", separator.ToString())
                };

                // HO TROVATO ORIGINAL DOC DATA VUOTO!!!
                if (row.original_DOCDATA.Trim() == String.Empty)
                    row.original_DOCDATA = row.dtOrdine;

                Boolean res= Serialize_ORDERSP(row);
               
                if(res)
                {
                   Delete_row(row.original_DOCID);
                }

            }
            cn.Close();


        }

        private void Delete_row(String original_DOCID)
        {

            OdbcConnection cn = new OdbcConnection();
            OdbcCommand cmd = new OdbcCommand();

            String ConnectionString = Properties.Settings.Default.CnStriSeries;
            try
            {
                cn.ConnectionString = ConnectionString;
                cmd.Connection = cn;

                String query = @"DELETE FROM CONORD0F WHERE CDOCID = '" + original_DOCID + "'";

                cmd.CommandText = query;
                cn.Open();
                Int32 r_affected = cmd.ExecuteNonQuery();
                ev.WriteEventToMyLog(Source + "CONORD0F Delete row", "riga con doc id: " + original_DOCID + " CANCELLATA!", EventLogEntryType.Information, 77);
            }
            catch (Exception ex)
            {

                ev.WriteEventToMyLog(Source + "CONORD0F Delete row", "Errore:\n" + ex.ToString(), EventLogEntryType.Error, 99);

            }


        }

        public Boolean Serialize_ORDERSP(CONORD0F ConfermaOrdine)
        {

            DateTime Consegna_DateTime = DateTime.ParseExact(ConfermaOrdine.dtConsegna, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);//Convert.ToDateTime(ConfermaOrdine.dtConsegna.Substring(0, 4) + "." + ConfermaOrdine.dtConsegna.Substring(4, 2) + "." + ConfermaOrdine.dtConsegna.Substring(6, 2));

            DateTime docdata_datetime = DateTime.ParseExact(ConfermaOrdine.original_DOCDATA, "dd/mm/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);// Convert.ToDateTime(ConfermaOrdine.original_DOCDATA.Substring(0, 4) + "." + ConfermaOrdine.original_DOCDATA.Substring(4, 2) + "." + ConfermaOrdine.original_DOCDATA.Substring(6, 2));

            String dataOrdrsp = String.Empty;

            DateTime dt = DateTime.Now;
            // formattazione data per nome file univoco
            dataOrdrsp = String.Format("{0:yyyy.MM.dd_HH.mm.ss.fff.zzz}", dt);
            dataOrdrsp = dataOrdrsp.Replace(":", "");

            TextWriter writer = new StreamWriter(ServicePathOut + @"Ordrsp_" + dataOrdrsp + ".xml");
            try
            {
                XmlSerializer serialize = new XmlSerializer(typeof(pAXORDRSP));
               
                pAXORDRSP ordersp = new pAXORDRSP();

                // primo livello
                pAXORDRSPPAXTransmissionHeader trh = new pAXORDRSPPAXTransmissionHeader();
                pAXORDRSPPAXTransmissionSender trs = new pAXORDRSPPAXTransmissionSender();//  
                pAXORDRSPPAXTransmissionReceiver trr = new pAXORDRSPPAXTransmissionReceiver();// sono 2
                pAXORDRSPPAXDocumentHeader dch = new pAXORDRSPPAXDocumentHeader();

                //secondo livello ci sono 5 address
                pAXORDRSPPAXDocumentHeaderPAXAddress add = new pAXORDRSPPAXDocumentHeaderPAXAddress();//ci sono 4 address
                pAXORDRSPPAXDocumentHeaderPAXReference rfr = new pAXORDRSPPAXDocumentHeaderPAXReference();
                pAXORDRSPPAXDocumentHeaderPAXDateTimes dtt = new pAXORDRSPPAXDocumentHeaderPAXDateTimes();//2 datetimes
                pAXORDRSPPAXDocumentHeaderPAXTax tax = new pAXORDRSPPAXDocumentHeaderPAXTax();
                pAXORDRSPPAXDocumentHeaderPAXCurrency cur = new pAXORDRSPPAXDocumentHeaderPAXCurrency();
                pAXORDRSPPAXDocumentHeaderPAXIncoTerm inc = new pAXORDRSPPAXDocumentHeaderPAXIncoTerm();
                pAXORDRSPPAXDocumentHeaderPAXShippingType sht = new pAXORDRSPPAXDocumentHeaderPAXShippingType();
                pAXORDRSPPAXDocumentHeaderPAXText txt = new pAXORDRSPPAXDocumentHeaderPAXText();
                pAXORDRSPPAXDocumentHeaderPAXDocumentLine dcl = new pAXORDRSPPAXDocumentHeaderPAXDocumentLine();
                pAXORDRSPPAXDocumentHeaderPAXSummation sum = new pAXORDRSPPAXDocumentHeaderPAXSummation();

                // terzo livello in document line
                pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXReference dcl_ref = new pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXReference();
                pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXDateTimes dcl_dtt = new pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXDateTimes();
                pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXSummation dcl_sum = new pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXSummation();



                ordersp.pAXTransmissionHeader = new pAXORDRSPPAXTransmissionHeader[1];
                ordersp.pAXTransmissionHeader[0] = trh;

                trh.TransmissionNormInternal = "pAX";
                trh.MessageTypeInternal = "ORDRSP";
                trh.TransmissionNo = 0;
                trh.TransmissionDateTime = DateTime.Now;



                ordersp.pAXTransmissionSender = new pAXORDRSPPAXTransmissionSender[1];
                ordersp.pAXTransmissionSender[0] = trs;

                trs.SenderType = "PartnerID";
                trs.SenderID = "705452";


                ordersp.pAXTransmissionReceiver = new pAXORDRSPPAXTransmissionReceiver[2];
                ordersp.pAXTransmissionReceiver[0] = trr;

                trr.ReceiverType = "GLN";
                trr.ReceiverID = "7612158000004";
                ordersp.pAXTransmissionReceiver[1] = trr;
                trr.ReceiverType = "PartnerID";
                trr.ReceiverID = "705452";

                ordersp.pAXDocumentHeader = new pAXORDRSPPAXDocumentHeader[1];
                ordersp.pAXDocumentHeader[0] = dch;

                dch.DocHeaderID = ConfermaOrdine.original_DOCID;
                dch.DocumentType = "U";
                dch.ParentID = ConfermaOrdine.original_DOCID;
                dch.InternalDocID = Convert.ToInt32(ConfermaOrdine.prefisso + ConfermaOrdine.nOrdine);//?
                dch.DocumentDate = Consegna_DateTime.Date;
                dch.DocumentNumber = ConfermaOrdine.prefisso + ConfermaOrdine.nOrdine;
                dch.ParticipantReceiverIdentifier = "7612158000004";



                dch.pAXReference = new pAXORDRSPPAXDocumentHeaderPAXReference[1];
                dch.pAXReference[0] = rfr;

                rfr.ReferenceType = "Order";
                rfr.ReferenceID = ConfermaOrdine.original_DOCNR;
                rfr.ReferenceDate = Convert.ToDateTime(docdata_datetime);
                rfr.ParentID= ConfermaOrdine.original_RIGID;


                dch.pAXDocumentLine = new pAXORDRSPPAXDocumentHeaderPAXDocumentLine[1];
                dch.pAXDocumentLine[0] = dcl;

                dcl.DocLineID = ConfermaOrdine.original_RIGID;
                dcl.ParentID = ConfermaOrdine.original_DOCID;
                dcl.PositionSequentialNo = Convert.ToInt32(ConfermaOrdine.original_POSSEQNBR);
                dcl.PositionNoSender = Convert.ToDecimal(ConfermaOrdine.original_POSNOSENDER);
                dcl.PositionType = "PartLine";
                dcl.SubPosition = false;
                dcl.EAN = ConfermaOrdine.original_EAN.Trim();
                dcl.PartDesc1 = ConfermaOrdine.descrizione;
                dcl.PartDesc2 = "";
                dcl.PartDesc3 = "";
                dcl.PartDesc4 = "";
                dcl.Variant = false;
                dcl.PackagingQuantity = Convert.ToDecimal(ConfermaOrdine.qta);
                dcl.PackagingQuantityUnit = "0";
                dcl.Quantity = Convert.ToDecimal(ConfermaOrdine.qta);
                dcl.WeightNet = 0;
                dcl.WeightGross = 0;

               

                // ultimo livello di document line
                dcl.pAXReference = new pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXReference[1];
                dcl.pAXReference[0] = dcl_ref;

                dcl_ref.ParentID = ConfermaOrdine.original_RIGID;
                dcl_ref.ReferenceType = "Order";
                dcl_ref.ReferenceID = ConfermaOrdine.original_DOCNR;
                dcl_ref.AdditionalReferenceID = "1";
                dcl_ref.ReferenceDate =docdata_datetime.Date;



                dcl.pAXDateTimes = new pAXORDRSPPAXDocumentHeaderPAXDocumentLinePAXDateTimes[3];
                dcl.pAXDateTimes[0] = dcl_dtt;

                dcl_dtt.ParentID = ConfermaOrdine.original_RIGID;
                dcl_dtt.DateTimeType = "DeliveryDate";
                dcl_dtt.DateAndTime = Consegna_DateTime.Date;



                serialize.Serialize(writer, ordersp);
               
            }
            catch (Exception ex)
            {
                writer.Close();
                ev.WriteEventToMyLog(Source + "Serialize ORDERSP", "Errore:\n" + ex.ToString() + "\n--\n" + ex.StackTrace +"\n--\n" + ex.Message , EventLogEntryType.Error, 99);
                return false;
            }
            writer.Close();
            return true; 

        }
        #endregion

        #region BOLLE DESADV
        public void read_BOLLE00F()
        {
            ev.WriteEventToMyLog(Source + "Out Bolle", "Lettura e conversione Bolle", EventLogEntryType.Information, 7);

            OdbcConnection cn = new OdbcConnection();
            OdbcCommand cmd = new OdbcCommand();

            String ConnectionString = Properties.Settings.Default.CnStriSeries;




            cn.ConnectionString = ConnectionString;
            cmd.Connection = cn;

            

            String query = @"SELECT * FROM BOLLE00F";



            cmd.CommandText = query;
            cn.Open();
            OdbcDataReader rd = cmd.ExecuteReader();

            while (rd.Read())
            {

                BOLLE00F row = new BOLLE00F
                {
                    nInterno = rd.GetValue(0).ToString(),
                    rBolla = rd.GetValue(1).ToString(),
                    Raggr_Magazzino = rd.GetValue(2).ToString(),
                    Gruppo_numero = rd.GetValue(3).ToString(),
                    nBolla = rd.GetValue(4).ToString(),
                    dtBolla = rd.GetValue(5).ToString(),
                    articolo = rd.GetValue(6).ToString(),
                    descrizione = rd.GetValue(7).ToString(),
                    qta = rd.GetValue(8).ToString(),
                    original_DOCID = rd.GetValue(9).ToString(),
                    original_DOCNR = rd.GetValue(10).ToString(),
                    original_DOCDATA = rd.GetValue(11).ToString(),
                    original_RIGID = rd.GetValue(12).ToString(),
                    original_EAN = rd.GetValue(13).ToString(),
                    original_POSSEQNBR = rd.GetValue(14).ToString(),
                    original_POSNOSENDER = rd.GetValue(15).ToString().Replace(".", separator.ToString()).Replace(",", separator.ToString())
                };

                // HO TROVATO ORIGINAL DOC DATA VUOTO!!!
                if (row.original_DOCDATA.Trim() == String.Empty)
                    row.original_DOCDATA = row.dtBolla;


                Boolean res = Serialize_DESADV(row);

                if (res)
                {
                   Delete_row_BOLLE00F(row.original_DOCID);
                }
            }
            cn.Close();


        }


        private void Delete_row_BOLLE00F(String original_DOCID)
        {

            OdbcConnection cn = new OdbcConnection();
            OdbcCommand cmd = new OdbcCommand();

            String ConnectionString = Properties.Settings.Default.CnStriSeries;

            try
            {
                cn.ConnectionString = ConnectionString;
                cmd.Connection = cn;

                String query = @"DELETE FROM BOLLE00F WHERE BDOCID = '" + original_DOCID + "'";

                cmd.CommandText = query;
                cn.Open();
                Int32 r_affected = cmd.ExecuteNonQuery();
                ev.WriteEventToMyLog(Source + "BOLLE00F Delete row", "riga con doc id: " + original_DOCID + " CANCELLATA!", EventLogEntryType.Information, 77);
            }
            catch ( Exception ex)
            {

                ev.WriteEventToMyLog(Source + "BOLLE00F Delete row", "Errore:\n" + ex.ToString(), EventLogEntryType.Error, 99);

            }



        }

        public Boolean Serialize_DESADV(BOLLE00F bolla)
        {
            DateTime bolla_Datetime = DateTime.ParseExact(bolla.dtBolla, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);
            DateTime docdata_datetime = DateTime.ParseExact(bolla.original_DOCDATA, "dd/mm/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);
            //DateTime bolla_Datetime = Convert.ToDateTime(bolla.dtBolla.Substring(0, 4) +"."+ bolla.dtBolla.Substring(4, 2) + "." + bolla.dtBolla.Substring(6, 2));
           // DateTime docdata_datetime = Convert.ToDateTime(bolla.original_DOCDATA.Substring(0, 4) + "." + bolla.original_DOCDATA.Substring(4, 2) + "." + bolla.original_DOCDATA.Substring(6, 2));


            String dataDesadv = String.Empty;

            DateTime dt = DateTime.Now;
            // formattazione data per nome file univoco
            dataDesadv = String.Format("{0:yyyy.MM.dd_HH.mm.ss.fff.zzz}", dt);
            dataDesadv = dataDesadv.Replace(":", "");
            TextWriter writer = new StreamWriter(ServicePathOut + @"Desadv_" + dataDesadv + ".xml");
            try
            {
                XmlSerializer serialize = new XmlSerializer(typeof(pAXDESADV));
                
                pAXDESADV desadv = new pAXDESADV();

  
                desadv.pAXTransmissionHeader = new pAXDESADVPAXTransmissionHeader[1];
                desadv.pAXTransmissionHeader[0] = new pAXDESADVPAXTransmissionHeader
                {

                    TransmissionNormInternal = "pAX",
                    MessageTypeInternal = "DESADV",
                    TransmissionNo = 0,
                    TransmissionDateTime = DateTime.Now
                };





                desadv.pAXTransmissionSender = new pAXDESADVPAXTransmissionSender[1];
                desadv.pAXTransmissionSender[0] = new pAXDESADVPAXTransmissionSender
                {
                    SenderType = "PartnerID",
                    SenderID = "705452"
                };




                desadv.pAXTransmissionReceiver = new pAXDESADVPAXTransmissionReceiver[2];

                desadv.pAXTransmissionReceiver[0] = new pAXDESADVPAXTransmissionReceiver
                {
                    ReceiverType = "PartnerID",
                    ReceiverID = "7612158000004"
                };


                desadv.pAXTransmissionReceiver[1] = new pAXDESADVPAXTransmissionReceiver
                {
                    ReceiverType = "PartnerID",
                    ReceiverID = "705452"
                };


                desadv.pAXDocumentHeader = new pAXDESADVPAXDocumentHeader[1];
                desadv.pAXDocumentHeader[0] = new pAXDESADVPAXDocumentHeader
                {
                    DocHeaderID = bolla.original_DOCID,
                    DocumentType = "L",
                    // ParentID = bolla.original_DOCID,
                    InternalDocID = Convert.ToInt32(bolla.nInterno),
                    DocumentDate = bolla_Datetime.Date,
                    DocumentNumber = bolla.original_DOCNR,
                    ParticipantReceiverIdentifier = "7612158000004"




                };
                desadv.pAXDocumentHeader[0].pAXAddress = new pAXDESADVPAXDocumentHeaderPAXAddress[2];
                desadv.pAXDocumentHeader[0].pAXAddress[0] = new pAXDESADVPAXDocumentHeaderPAXAddress
                {
                    ParentID = bolla.original_DOCID,
                    AddressType = "CONTACT-ADDRESS",
                    Name1 = "MAMOLI",
                    EMail = "info@mamoli.it",
                    Phone1 = "xxxxxxxxxxx"

                };
                desadv.pAXDocumentHeader[0].pAXAddress[1] = new pAXDESADVPAXDocumentHeaderPAXAddress
                {
                    ParentID = bolla.original_DOCID,
                    AddressType = "DELIVERY-ADDRESS",
                    Name1 = "COMPANY ABCD",
                    Name2 = "",
                    Name3 = "",
                    Street1 = "street",
                    City = "city",
                    PostCode = "7001",
                    State = "IT"

                };



                desadv.pAXDocumentHeader[0].pAXReference = new pAXDESADVPAXDocumentHeaderPAXReference[1];
                desadv.pAXDocumentHeader[0].pAXReference[0] = new pAXDESADVPAXDocumentHeaderPAXReference
                {
                    ReferenceType = "PurchOrder",
                    ReferenceID = bolla.nBolla,
                    ReferenceDate = Convert.ToDateTime(docdata_datetime)
                };



                desadv.pAXDocumentHeader[0].pAXDateTimes = new pAXDESADVPAXDocumentHeaderPAXDateTimes[2];
                desadv.pAXDocumentHeader[0].pAXDateTimes[0] = new pAXDESADVPAXDocumentHeaderPAXDateTimes
                {
                    ParentID = bolla.original_DOCID,
                    DateTimeType = "DeliveryDate",
                    DateAndTime = bolla_Datetime
                };

                desadv.pAXDocumentHeader[0].pAXDateTimes[1] = new pAXDESADVPAXDocumentHeaderPAXDateTimes
                {
                    ParentID = bolla.original_DOCID,
                    DateTimeType = "ShippingDate",
                    DateAndTime = bolla_Datetime
                };

                ///////////////

                pAXDESADVPAXDocumentHeaderPAXDocumentHeader dch_dch = new pAXDESADVPAXDocumentHeaderPAXDocumentHeader();
                desadv.pAXDocumentHeader[0].pAXDocumentHeader = new pAXDESADVPAXDocumentHeaderPAXDocumentHeader[1];
                desadv.pAXDocumentHeader[0].pAXDocumentHeader[0] = dch_dch;

                // sono dati interni duplicati dall'header fuori
                dch_dch.DocHeaderID = bolla.original_DOCID;
                dch_dch.DocumentType = "L";
                dch_dch.ParentID = bolla.original_DOCID;
                dch_dch.InternalDocID = Convert.ToInt32(bolla.nInterno);
                dch_dch.DocumentDate = bolla_Datetime.Date;
                dch_dch.DocumentNumber = bolla.original_DOCNR;
                dch_dch.ParticipantReceiverIdentifier = "7612158000004";

                dch_dch.pAXAddress = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXAddress[1];
                dch_dch.pAXAddress[0] = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXAddress
                {
                    ParentID = bolla.original_DOCID,
                    AddressType = "DELIVERY-ADDRESS",
                    Name1 = "COMPANY ABCD",
                    Name2 = "",
                    Name3 = "",
                    Street1 = "street",
                    City = "city",
                    PostCode = "7001",
                    State = "IT"

                };

                //////////////////////



                dch_dch.pAXDateTimes = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDateTimes[2];

                dch_dch.pAXDateTimes[0] = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDateTimes
                {
                    ParentID = bolla.original_DOCID,
                    DateTimeType = "DeliveryDate",
                    DateAndTime = bolla_Datetime
                };
                dch_dch.pAXDateTimes[1] = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDateTimes
                {
                    ParentID = bolla.original_DOCID,
                    DateTimeType = "ShippingDate",
                    DateAndTime = bolla_Datetime

                };
                dch_dch.pAXReference = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXReference[1];
                dch_dch.pAXReference[0] = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXReference
                {
                    ParentID = bolla.original_DOCID,
                    ReferenceType = "PurchOrder",
                    ReferenceID = bolla.original_DOCNR,
                    // AdditionalReferenceID = bolla.nInterno,
                    ReferenceDate = bolla_Datetime.Date
                };


                /// DOCUMENT LINE
                dch_dch.pAXDocumentLine = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDocumentLine[1];
                dch_dch.pAXDocumentLine[0] = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDocumentLine
                {
                    DocLineID = bolla.original_RIGID,
                    ParentID = bolla.original_DOCID,
                    PositionSequentialNo = Convert.ToInt32(bolla.original_POSSEQNBR),
                    PositionNoSender = Convert.ToDecimal(bolla.original_POSNOSENDER),
                    SubPosition = false,
                    EAN = bolla.original_EAN,
                    PartDesc1 = bolla.descrizione,
                    PartDesc2="",
                    PartDesc3="",
                    PartDesc4="",
                    PackagingQuantityUnit="0",
                    WeightNet=0,
                    WeightGross=0,
                    PackagingQuantity= Convert.ToDecimal(bolla.qta),
                    Quantity = Convert.ToDecimal(bolla.qta)                    

                };

                dch_dch.pAXDocumentLine[0].pAXReference = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDocumentLinePAXReference[1];
                dch_dch.pAXDocumentLine[0].pAXReference[0] = new pAXDESADVPAXDocumentHeaderPAXDocumentHeaderPAXDocumentLinePAXReference
                {
                    ParentID = bolla.original_RIGID,
                    ReferenceType = "PurchOrder",
                    ReferenceID = bolla.original_DOCNR,
                    AdditionalReferenceID = "1",
                    ReferenceDate = bolla_Datetime.Date

                };
                ////////////




                serialize.Serialize(writer, desadv);

               

            }
            catch (Exception ex)
            {
                writer.Close();
                ev.WriteEventToMyLog(Source + "Serialize DESADV", "Errore:\n" + ex.ToString() + "\n--\n" + ex.StackTrace + "\n--\n" + ex.Message, EventLogEntryType.Error, 99);
                return false;
                
            }

            writer.Close();
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

    public class CONORD0F
    {
        public String prefisso;
        public String nOrdine;
        public String dtOrdine;
        public String rigOrdine;
        public String articolo;
        public String descrizione;
        public String qta;
        public String dtConsegna;
        public String original_DOCID;
        public String original_DOCNR;
        public String original_DOCDATA;
        public String original_RIGID;
        public String original_EAN;
        public String original_POSSEQNBR;
        public String original_POSNOSENDER;

    }

    public class BOLLE00F
    {
        public String nInterno;
        public String rBolla;
        public String Raggr_Magazzino;
        public String Gruppo_numero;
        public String nBolla;
        public String dtBolla;
        public String articolo;
        public String descrizione;
        public String qta;
        public String original_DOCID;
        public String original_DOCNR;
        public String original_DOCDATA;
        public String original_RIGID;
        public String original_EAN;
        public String original_POSSEQNBR;
        public String original_POSNOSENDER;

    }



    public class order_testata
    {
        public String DocHeaderID;
        public String DocumentType;//    
        public String DocumentDate;//    
        public String DocumentNumber;//   
        public String DesiredDate;// 

    }

    public class order_riga
    {
        public String DocLineID;
        public String ParentID;
        public String PartIDSender;// 
        public String PositionNoSender;//     
        public String EAN;//    
        public String PartDesc;
        public String Quantity;//   
        public String PositionSequentialNo;
    }
}
