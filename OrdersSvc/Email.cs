using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.IO;
using System.Text;

namespace OrdersSvc
{
    public class RESPONSE_SMTP
    {
        public Boolean Issent { get; set; }
        public String Error { get; set; }
    }
    public class Email
    {


        #region Send Email

        public RESPONSE_SMTP SendSMTP(string Server, string Port, string Username, string Password, string Subject,
            string Body, List<String> To, string From, string icona, Boolean CCN, Boolean SSL)
        {
            //Set up SMTP client
            RESPONSE_SMTP result = new RESPONSE_SMTP();

            SmtpClient client = new SmtpClient();
            client.Host = Server;
            client.Port = int.Parse(Port);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.Credentials = new NetworkCredential(Username, Password);
            client.EnableSsl = SSL;



            MailMessage message = new MailMessage();



            // gestione destinatari

            foreach (String dst in To)
            {
                if (!CCN)
                    if (dst.Trim() != "")// controllo che l'address non sia vuoto
                        message.To.Add(dst.Trim()); // parsing dei destinatari togliendo gli eventuali spazi
                    else
                    if (dst.Trim() != "")
                        message.Bcc.Add(dst.Trim());// CCN per Circolari
            }


            message.From = new MailAddress(From);
            message.Subject = Subject;


            message.IsBodyHtml = true; //HTML email
            message.Body = Body;

            //Attempt to send the email

            try
            {
                client.Send(message);

                result.Issent = true;
                result.Error = "OK";
                return result;

                //  MessageBox.Show("Invio EMAIL Avvenuto correttamente!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }
            catch (SmtpException ex)
            {

                result.Issent = false;
                result.Error = ex.Message;
                return result;
                // MessageBox.Show("There was an error while sending the message:\n\n" + ex.Message);
            }
        }


        public Boolean SendEmailWebDav(string p_strServer, string p_strAlias, string strSendTo,
        string strSendSubject, string strSendBody, string p_strUserName, string p_strPassword)
        {
            HttpWebRequest PUTRequest = default(HttpWebRequest);
            WebResponse PUTResponse = default(WebResponse);
            HttpWebRequest MOVERequest = default(HttpWebRequest);
            WebResponse MOVEResponse = default(WebResponse);
            string strMailboxURI = "";
            string strSubURI = "";
            string strTempURI = "";
            string strTo = strSendTo;
            string strSubject = strSendSubject;
            string strText = strSendBody;
            string strBody = "";
            byte[] bytes = null;
            Stream PUTRequestStream = null;

            try
            {
                strMailboxURI = p_strServer + "/exchange/" + p_strAlias;

                strSubURI = p_strServer + "/exchange/" + p_strAlias
                          + "/##DavMailSubmissionURI##/";

                strTempURI = p_strServer + "/exchange/" + p_strAlias
                           + "/" + "bozze" + "/" + strSubject + ".eml";

                strBody = "To: " + strTo + "\n" +
                "Subject: " + strSubject + "\n" +
                "Date: " + System.DateTime.Now +
                "X-Mailer: test mailer" + "\n" +
                "MIME-Version: 1.0" + "\n" +
                "Content-Type: text/plain;" + "\n" +
                "Charset = \"iso-8859-1\"" + "\n" +
                "Content-Transfer-Encoding: 7bit" + "\n" +
                "\n" + strText;

                PUTRequest = (HttpWebRequest)HttpWebRequest.Create(strTempURI);
                PUTRequest.Credentials = new NetworkCredential(p_strUserName, p_strPassword);
                PUTRequest.Method = "PUT";
                bytes = Encoding.UTF8.GetBytes((string)strBody);
                PUTRequest.ContentLength = bytes.Length;
                PUTRequestStream = PUTRequest.GetRequestStream();
                PUTRequestStream.Write(bytes, 0, bytes.Length);
                PUTRequestStream.Close();
                PUTRequest.ContentType = "message/rfc822";
                PUTResponse = (HttpWebResponse)PUTRequest.GetResponse();
                MOVERequest = (HttpWebRequest)HttpWebRequest.Create(strTempURI);
                MOVERequest.Credentials = new NetworkCredential(p_strUserName, p_strPassword);
                MOVERequest.Method = "MOVE";
                MOVERequest.Headers.Add("Destination", strSubURI);
                MOVEResponse = (HttpWebResponse)MOVERequest.GetResponse();
                Console.WriteLine("Message successfully sent.");

                // Clean up.
                PUTResponse.Close();
                MOVEResponse.Close();

            }
            catch (Exception)
            {

                return false;
            }
            return true;
        }
        #endregion




     }
}
