using System; 
using System.Diagnostics;

namespace OrdersSvc
{
    public class LogEventi
    {
        protected const string EventLogName = "Mamoli Orders";

        // CONTROLLO SE ESISTE SE NO LO CREA
        public bool CheckSourceExists(string source)
        {
            if (EventLog.SourceExists(source))
            {
                EventLog evLog = new EventLog { Source = source };
                if (evLog.Log != EventLogName)
                {
                    EventLog.DeleteEventSource(source);
                }
            }

            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, EventLogName);
                EventLog.WriteEntry(source, String.Format("Event Log Created '{0}'/'{1}'", EventLogName, source), EventLogEntryType.Information);
            }

            return EventLog.SourceExists(source);// RITORNA TRUE O FALSE SE ESISTE O MENO
        }

        // SCRITTURA SU EVENTLOG PERSONALIZZATO
        public void WriteEventToMyLog(string source, string text, EventLogEntryType type, Int32 eventid)
        {
            if (CheckSourceExists(source))// SE ESISTE SCRIVO MA SE NO LO CREA E SCRIVE
            {
                try
                {
                    if (text.Length > 32000)
                    {
                        text = text.Substring(0, 3000) + "...";

                        EventLog.WriteEntry(source, text.ToString(), type, eventid);
                    }
                    else
                        EventLog.WriteEntry(source, text, type, eventid);

                }
                catch (Exception ex)
                {

                    EventLog.WriteEntry(source,ex.Message, type, eventid);
                }
            }
        }
    }
}
