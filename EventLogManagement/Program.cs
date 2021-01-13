using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EventLogManagement
{
    class Program
    {
        static string dosya = "";
        static bool exitSystem = false;
        //static int counter = 0;
        static int linecounter = 0;
        static string workingDirectory = Environment.CurrentDirectory;
        static string projectPath = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
        static string conString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename="+ projectPath + @"\LogMng.mdf;Integrated Security=True";

        #region Trap application termination
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            Console.WriteLine("Exiting system due to external CTRL-C, or process kill, or shutdown");
            //string line = "Okunan Dosya Adı : " + dosya + ".\n " + linecounter + ". Satırda kaldı.";
            //System.IO.File.WriteAllText(@"C:\XMLFiles\deneme.txt", line);
            string queryString = "UPDATE dbo.Log SET LastLineNumber=@lln WHERE FileName=@fnm";
            var sqlConn = new SqlConnection(conString);
            using (SqlCommand command = new SqlCommand(queryString, sqlConn))
            {
                command.Parameters.AddWithValue("@lln", linecounter);
                command.Parameters.AddWithValue("@fnm", dosya);
                sqlConn.Open();
                var transaction = sqlConn.BeginTransaction("SampleTransaction");
                try
                {
                    // Start a local transaction.
                    // Must assign both transaction object and connection
                    // to Command object for a pending local transaction
                    command.Connection = sqlConn;
                    command.Transaction = transaction;
                    command.ExecuteNonQuery();
                    // Attempt to commit the transaction.
                    transaction.Commit();
                    Console.WriteLine("Both records are written to database.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                    Console.WriteLine("  Message: {0}", ex.Message);

                    // Attempt to roll back the transaction.
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        // This catch block will handle any errors that may have occurred
                        // on the server that would cause the rollback to fail, such as
                        // a closed connection.
                        Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                        Console.WriteLine("  Message: {0}", ex2.Message);
                    }
                }
                sqlConn.Close();
            }

            //do your cleanup here
            Thread.Sleep(5000); //simulate some cleanup delay

            Console.WriteLine("Cleanup complete");

            //allow main to run off
            exitSystem = true;

            //shutdown right away so there are no lingering threads
            Environment.Exit(-1);

            return true;
        }
        #endregion


        public static void ParallelExample()
        {
            var timer = new Stopwatch();
            timer.Start();
            //SqlConnection sqlConn = new SqlConnection(conString);
            string queryString = "INSERT INTO dbo.Log (FileName,TotalLineCount) VALUES (@fnm,@tlc)";
            string queryString2 = "UPDATE dbo.Log SET LastLineNumber=@lln WHERE FileName=@fnm";
            var ext = new List<string> { "evtx" };
            SqlTransaction transaction;
            //Regex containsABadCharacter = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]");
            //foreach (string file in Directory.GetFiles(@"C:\Windows\System32\winevt\Logs"))
            Parallel.ForEach(Directory.GetFiles(@"C:\Windows\System32\winevt\Logs", "*.*", SearchOption.AllDirectories)
                .Where(s => ext.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant())).Select(f => Path.GetFileName(f)), (file) =>
                {
                    {
                        if (!file.Contains('%'))
                        {
                            int totallinecounter = 0;
                            using (var reader = new EventLogReader(@"C:\Windows\System32\winevt\Logs\" + file, PathType.FilePath))
                            {
                                EventRecord record;
                                while ((record = reader.ReadEvent()) != null)
                                {
                                    totallinecounter++;
                                }
                            }

                            string xml_filename = file.Replace(".evtx", ".xml");
                            FileInfo fileInfo = new FileInfo(@"C:\XML Files\Multithreading\" + xml_filename);


                            SqlConnection sqlConn = new SqlConnection(conString);
                            SqlTransaction transaction;
                            using (SqlCommand command = new SqlCommand(queryString, sqlConn))
                            {
                                command.Parameters.AddWithValue("@fnm", xml_filename);
                                command.Parameters.AddWithValue("@tlc", totallinecounter);
                                sqlConn.Open();
                                transaction = sqlConn.BeginTransaction("SampleTransaction");
                                try
                                {
                                    // Start a local transaction.
                                    // Must assign both transaction object and connection
                                    // to Command object for a pending local transaction
                                    command.Connection = sqlConn;
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                    // Attempt to commit the transaction.
                                    transaction.Commit();
                                    Console.WriteLine("Both records are written to database.");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                                    Console.WriteLine("  Message: {0}", ex.Message);

                                    // Attempt to roll back the transaction.
                                    try
                                    {
                                        transaction.Rollback();
                                    }
                                    catch (Exception ex2)
                                    {
                                        // This catch block will handle any errors that may have occurred
                                        // on the server that would cause the rollback to fail, such as
                                        // a closed connection.
                                        Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                                        Console.WriteLine("  Message: {0}", ex2.Message);
                                    }
                                }
                                sqlConn.Close();
                            }


                            using (var reader = new EventLogReader(@"C:\Windows\System32\winevt\Logs\" + file, PathType.FilePath))
                            {
                                EventRecord record;
                                while ((record = reader.ReadEvent()) != null)
                                {
                                    linecounter++;
                                    using (record)
                                    {
                                        //Console.WriteLine("{0}\n", record.ToXml());
                                        //Thread.Sleep(1000);

                                        SqlConnection sqlConn2 = new SqlConnection(conString);
                                        SqlTransaction transaction2;
                                        using (SqlCommand command2 = new SqlCommand(queryString2, sqlConn))
                                        {
                                            command2.Parameters.AddWithValue("@lln", linecounter);
                                            command2.Parameters.AddWithValue("@fnm", xml_filename);
                                            sqlConn2.Open();
                                            transaction2 = sqlConn2.BeginTransaction("SampleTransaction");
                                            try
                                            {
                                                // Start a local transaction.
                                                // Must assign both transaction object and connection
                                                // to Command object for a pending local transaction
                                                command2.Connection = sqlConn2;
                                                command2.Transaction = transaction2;
                                                command2.ExecuteNonQuery();
                                                // Attempt to commit the transaction.
                                                transaction2.Commit();
                                                Console.WriteLine("Both records are written to database.");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                                                Console.WriteLine("  Message: {0}", ex.Message);

                                                // Attempt to roll back the transaction.
                                                try
                                                {
                                                    transaction2.Rollback();
                                                }
                                                catch (Exception ex2)
                                                {
                                                    // This catch block will handle any errors that may have occurred
                                                    // on the server that would cause the rollback to fail, such as
                                                    // a closed connection.
                                                    Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                                                    Console.WriteLine("  Message: {0}", ex2.Message);
                                                }
                                            }
                                            sqlConn2.Close();
                                        }

                                        //if (!File.Exists(fileInfo.FullName))
                                        //{
                                        //    File.Create(fileInfo.FullName).Close();
                                        //    File.WriteAllText(fileInfo.FullName, record.ToXml());
                                        //}
                                        //else
                                        //{
                                        //    try
                                        //    {
                                        //        File.AppendAllTextAsync(fileInfo.FullName, record.ToXml());
                                        //    }
                                        //    catch (Exception ex)
                                        //    {
                                        //        Console.WriteLine(ex.Message);
                                        //    }
                                        //}

                                        //counter++;
                                    }
                                }
                            }
                            Console.WriteLine("FileName: {0}", file);
                        }
                    }
                });
            timer.Stop();
            TimeSpan timeTaken = timer.Elapsed;
            string foo = "Time taken: " + timeTaken.ToString(@"m\:ss\.fff");
            Console.WriteLine("Time: {0}, Counter: {1}", foo, linecounter);
        }

        public void SampleExample()
        {
            var timer = new Stopwatch();
            timer.Start();
            //string deneme = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //string deneme = AppDomain.CurrentDomain.BaseDirectory;
            SqlConnection sqlConn = new SqlConnection(conString);
            SqlConnection sqlConn2 = new SqlConnection(conString);
            string queryString = "INSERT INTO dbo.Log (FileName,TotalLineCount, CompletionStatus) VALUES (@fnm,@tlc, @cs)";
            //string queryString2 = "UPDATE dbo.Log SET LastLineNumber=@lln WHERE FileName=@fnm";
            var ext = new List<string> { "evtx" };
            //Regex containsABadCharacter = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]");
            SqlTransaction transaction;
            SqlTransaction transaction2;
            //foreach (string file in Directory.GetFiles(@"C:\Windows\System32\winevt\Logs"))
            //FileInfo fileInfo;
            FileInfo xmlfileInfo;
            string xml_filename;
            foreach (string file in Directory.GetFiles(@"C:\Windows\System32\winevt\Logs", "*.*", SearchOption.AllDirectories)
                .Where(s => ext.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant())).Select(f => Path.GetFileName(f)))
            {
                if (!file.Contains('%'))
                {
                    int totallinecounter = 0;
                    using (var reader = new EventLogReader(@"C:\Windows\System32\winevt\Logs\" + file, PathType.FilePath))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            totallinecounter++;
                        }
                    }

                    xml_filename = file.Replace(".evtx", ".xml");
                    dosya = xml_filename;
                    //fileInfo = new FileInfo(@"C:\XMLFiles\" + xml_filename);
                    xmlfileInfo = new FileInfo(@"C:\XMLFiles\audit-logs-last.xml");
                    using (SqlCommand command = new SqlCommand(queryString, sqlConn))
                    {
                        command.Parameters.AddWithValue("@fnm", xml_filename);
                        command.Parameters.AddWithValue("@tlc", totallinecounter);
                        command.Parameters.AddWithValue("@cs", false);
                        sqlConn.Open();
                        transaction = sqlConn.BeginTransaction("SampleTransaction");
                        try
                        {
                            // Start a local transaction.
                            // Must assign both transaction object and connection
                            // to Command object for a pending local transaction
                            command.Connection = sqlConn;
                            command.Transaction = transaction;
                            command.ExecuteNonQuery();
                            // Attempt to commit the transaction.
                            transaction.Commit();
                            Console.WriteLine("Both records are written to database.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                            Console.WriteLine("  Message: {0}", ex.Message);

                            // Attempt to roll back the transaction.
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception ex2)
                            {
                                // This catch block will handle any errors that may have occurred
                                // on the server that would cause the rollback to fail, such as
                                // a closed connection.
                                Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                                Console.WriteLine("  Message: {0}", ex2.Message);
                            }
                        }
                        sqlConn.Close();
                    }

                    using (var reader = new EventLogReader(@"C:\Windows\System32\winevt\Logs\" + file, PathType.FilePath))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            linecounter++;
                            using (record)
                            {
                                //sqlConn2 = new SqlConnection(conString);
                                //using (SqlCommand command2 = new SqlCommand(queryString2, sqlConn))
                                //{
                                //    command2.Parameters.AddWithValue("@lln", linecounter);
                                //    command2.Parameters.AddWithValue("@fnm", xml_filename);
                                //    sqlConn2.Open();
                                //    transaction2 = sqlConn2.BeginTransaction("SampleTransaction");
                                //    try
                                //    {
                                //        // Start a local transaction.
                                //        // Must assign both transaction object and connection
                                //        // to Command object for a pending local transaction
                                //        command2.Connection = sqlConn2;
                                //        command2.Transaction = transaction2;
                                //        command2.ExecuteNonQuery();
                                //        // Attempt to commit the transaction.
                                //        transaction2.Commit();
                                //        Console.WriteLine("Both records are written to database.");
                                //    }
                                //    catch (Exception ex)
                                //    {
                                //        Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                                //        Console.WriteLine("  Message: {0}", ex.Message);

                                //        // Attempt to roll back the transaction.
                                //        try
                                //        {
                                //            transaction2.Rollback();
                                //        }
                                //        catch (Exception ex2)
                                //        {
                                //            // This catch block will handle any errors that may have occurred
                                //            // on the server that would cause the rollback to fail, such as
                                //            // a closed connection.
                                //            Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                                //            Console.WriteLine("  Message: {0}", ex2.Message);
                                //        }
                                //    }
                                //    sqlConn2.Close();
                                //}

                                if (!File.Exists(xmlfileInfo.FullName))
                                {
                                    File.Create(xmlfileInfo.FullName).Close();
                                    File.WriteAllText(xmlfileInfo.FullName, record.ToXml());
                                }
                                else
                                {
                                    try
                                    {
                                        File.AppendAllTextAsync(xmlfileInfo.FullName, record.ToXml());
                                        Thread.Sleep(1000);
                                        //Console.CancelKeyPress += delegate {
                                        //    // call methods to clean up
                                        //    string line = "Okunan Dosya Adı : " + file + ".\n " + counter + ". Satırda kaldı.";
                                        //    System.IO.File.WriteAllText(@"C:\XMLFiles\deneme.txt", line);
                                        //    Environment.Exit(-1);
                                        //};
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                }

                                //counter++;
                            }
                        }
                        Console.WriteLine(file);
                    }
                }
            }
            timer.Stop();
            TimeSpan timeTaken = timer.Elapsed;
            string foo = "Time taken: " + timeTaken.ToString(@"m\:ss\.fff");
            Console.WriteLine("Time: {0}, Counter: {1}", foo, linecounter);
        }

        static void Main(string[] args)
        {
            // Some boilerplate to react to close window event, CTRL-C, kill, etc
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            //start your multi threaded program here
            Program p = new Program();


            //ParallelExample();

            p.SampleExample();

            //hold the console so it doesn’t run off the end
            while (!exitSystem)
            {
                Thread.Sleep(500);
            }

            //Console.WriteLine("Hello World");



            //while (true)
            //{
            //    string watchLog = "Security";
            //    EventLog myLog = new EventLog(watchLog);
            //    // set event handler
            //    myLog.EntryWritten += new EntryWrittenEventHandler(OnEntryWritten);
            //    myLog.EnableRaisingEvents = true;
            //}

            //SqlConnection sqlConn = new SqlConnection(conString);
            //string queryString = "INSERT INTO dbo.Log (Message,TimeGenerated,TimeWritten) VALUES (@msg,@gnr,@wrt)";
            //string eventLogName = "Security";
            //EventLog eventLog = new EventLog();
            //eventLog.Log = eventLogName;
            //string[] messages;
            //DateTime TimeGenerated;
            //DateTime TimeWritten;
            //SqlTransaction transaction;
            //int e1 = eventLog.Entries.Count - 1;

            //foreach (EventLogEntry log in eventLog.Entries)
            //{
            //    messages = log.Message.Split("\r\n\r\n");
            //    TimeGenerated = log.TimeGenerated;
            //    TimeWritten = log.TimeWritten;
            //    Console.WriteLine("{0}\n", messages[0]);
            //    using (SqlCommand command = new SqlCommand(queryString, sqlConn))
            //    {
            //        command.Parameters.AddWithValue("@msg", messages[0]);
            //        command.Parameters.AddWithValue("@gnr", TimeGenerated);
            //        command.Parameters.AddWithValue("@wrt", TimeWritten);
            //        sqlConn.Open();
            //        transaction = sqlConn.BeginTransaction("SampleTransaction");
            //        try
            //        {
            //            // Start a local transaction.
            //            // Must assign both transaction object and connection
            //            // to Command object for a pending local transaction
            //            command.Connection = sqlConn;
            //            command.Transaction = transaction;
            //            command.ExecuteNonQuery();
            //            // Attempt to commit the transaction.
            //            transaction.Commit();
            //            Console.WriteLine("Both records are written to database.");
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
            //            Console.WriteLine("  Message: {0}", ex.Message);

            //            // Attempt to roll back the transaction.
            //            try
            //            {
            //                transaction.Rollback();
            //            }
            //            catch (Exception ex2)
            //            {
            //                // This catch block will handle any errors that may have occurred
            //                // on the server that would cause the rollback to fail, such as
            //                // a closed connection.
            //                Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
            //                Console.WriteLine("  Message: {0}", ex2.Message);
            //            }
            //        }
            //        sqlConn.Close();
            //    }
            //}
            //Console.WriteLine("Bitti");
            //Console.ReadKey();
        }

        //private static void OnEntryWritten(object source, EntryWrittenEventArgs e)
        //{
        //    string watchLog = "Security";
        //    string logName = watchLog;
        //    int e1 = 0;
        //    EventLog log = new EventLog(logName);
        //    e1 = log.Entries.Count - 1; // last entry
        //    Console.WriteLine("{0}\n{1}\n\n", log.Entries[e1].Message, e1);
        //    log.Close();  // close log
        //}
    }
}
