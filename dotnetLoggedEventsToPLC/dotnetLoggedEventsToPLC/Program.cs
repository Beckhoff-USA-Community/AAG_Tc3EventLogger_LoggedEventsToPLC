// See https://aka.ms/new-console-template for more information
using TcEventLoggerAdsProxyLib;
using System.Globalization;

var logger = new TcEventLogger();

logger.MessageSent += (TcMessage message) => Console.WriteLine("Received Message: " + message.GetText(CultureInfo.CurrentCulture.LCID));
logger.AlarmRaised += (TcAlarm alarm) => Console.WriteLine("Alarm Raised: " + alarm.GetText(CultureInfo.CurrentCulture.LCID));
logger.AlarmCleared += (TcAlarm alarm, bool bRemove) => Console.WriteLine((bRemove ? "Alarm Cleared and was Confirmed: " : "Alarm Cleared: ") + alarm.GetText(CultureInfo.CurrentCulture.LCID));
logger.AlarmConfirmed += (TcAlarm alarm, bool bRemove) => Console.WriteLine((bRemove ? "Alarm Confirmed and was Cleared: " : "Alarm Confirmed: ") + alarm.GetText(CultureInfo.CurrentCulture.LCID));

logger.Connect("39.120.71.102.1.1"); //connect to localhost

Console.WriteLine("Press 'x' quit");
Console.WriteLine("Press 'l' to list last 100 logged events");
while (true)
{
 
    if (Console.KeyAvailable)
    {
        switch (Console.ReadKey(true).Key)
        {
            case ConsoleKey.L:
               ITcLoggedEventCollection tcLoggedEvents = logger.GetLoggedEvents(100);
                foreach (ITcLoggedEvent tcLoggedEvent in tcLoggedEvents)
                {

                    Console.WriteLine(tcLoggedEvent.EventType.ToString() + " : " + tcLoggedEvent.GetText(CultureInfo.CurrentCulture.LCID));

                }
                break;
            case ConsoleKey.A:
                
                break;
            case ConsoleKey.C:
               
                break;
            case ConsoleKey.X:
                
                break;
        }
    }
       
}