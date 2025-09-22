using System.Reflection;
using System.Security.Cryptography;

namespace TestTask
{
  class Program
  {
    //Time between synchronizations in miliseconds
    //60000 ms = 6 min
    private static int TimePeriod = 60000;

    //Paths for Source, Replica
    private static string Source = "";
    private static string Replica = "";
    private static string LogFile = "";

    //Files that were not copied successfully;
    private static List<string> FailedFiles = new List<string>();
    private static List<string> FailedToRemoveFiles = new List<string>();

    private static bool keepRunning = true;

    private static DateTime CheckupTime;

    static void Main(string[] args)
    {
      object _lock = new();

      //Code fragment that captures Ctrl+C. Used to break the loop.
      Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
      {
        e.Cancel = true;
        keepRunning = false;
        Logger.Instance.Log("\nProgram terminated.");
        Monitor.Pulse(_lock);
      };

      Console.WriteLine("Press Ctrl+C to stop the program.");

      //No arguments were passed. Trying to find "Source" and "Replica"
      //within folder of script's location.
      if (args.Length == 0)
      {
        string? ExecutablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (ExecutablePath == null)
        {
          Console.WriteLine(Environment.NewLine + "Error: Path of executable is invalid." + Environment.NewLine);
          Environment.Exit(0);
        }

        if (Directory.Exists(ExecutablePath + @"\Source"))
          Source = ExecutablePath + @"\Source";
        else
        {
          Console.WriteLine(Environment.NewLine + "Error: Source location is invalid." + Environment.NewLine);
          Environment.Exit(0);
        }
        if (Directory.Exists(ExecutablePath + @"\Replica"))
          Replica = ExecutablePath + @"\Replica";
        else
        {
          Console.WriteLine(Environment.NewLine + "Error: Replica location is invalid." + Environment.NewLine);
          Environment.Exit(0);
        }
      }

      //Arguments were passed. Synchronizing contents with
      //first argument as source and other as replica
      else if (args.Length == 2)
      {
        if (Directory.Exists(args[0]))
          Source = args[0];
        else
        {
          Console.WriteLine("\nError: Invalid path for source\n");
          Environment.Exit(0);
        }
        if (Directory.Exists(args[1]))
          Replica = args[1];
        else
        {
          Console.WriteLine("\nError: Invalid path for replica\n");
          Environment.Exit(0);
        }
      }
      else if (args.Length == 3)
      {
        if (Directory.Exists(args[0]))
          Source = args[0];
        else
        {
          Console.WriteLine("\nError: Invalid path for source\n");
          Environment.Exit(0);
        }
        if (Directory.Exists(args[1]))
          Replica = args[1];
        else
        {
          Console.WriteLine("\nError: Invalid path for replica\n");
          Environment.Exit(0);
        }
        try
        {
          TimePeriod = Int32.Parse(args[2]);
          if (TimePeriod < 1000)
          {
            Console.WriteLine("\nError: Internal value is too low\n");
            Environment.Exit(0);
          }
        }
        catch
        {
          Console.WriteLine("\nError: Interval value is invalid\n");
          Environment.Exit(0);
        }
      }
      else if (args.Length == 4)
      {
        if (Directory.Exists(args[0]))
          Source = args[0];
        else
        {
          Console.WriteLine("\nError: Invalid path for source\n");
          Environment.Exit(0);
        }
        if (Directory.Exists(args[1]))
          Replica = args[1];
        else
        {
          Console.WriteLine("\nError: Invalid path for replica\n");
          Environment.Exit(0);
        }
        if (Directory.Exists(args[3]))
        {
          LogFile = args[3];
        }
        else
        {
          Console.WriteLine("\nError: Invalid path for logging\n");
          Environment.Exit(0);
        }

        try
        {
          TimePeriod = Int32.Parse(args[2]);
          if (TimePeriod < 1000)
          {
            Console.WriteLine("\nError: Internal value is too low\n");
            Environment.Exit(0);
          }
        }
        catch
        {
          Console.WriteLine("\nError: Interval value is invalid\n");
          Environment.Exit(0);
        }

      }
      //Case with invalid number of arguments.
      else
      {
        Console.WriteLine("\nError: Too many or too few arguments.\n");
        Environment.Exit(0);
      }

      //Keeping hashes outside of loop due to safety reasons
      Span<byte> Hash1 = stackalloc byte[16];
      Span<byte> Hash2 = stackalloc byte[16];

      while (keepRunning)
      {
        Synchronisation(Directory.GetFiles(Source).Union(Directory.GetDirectories(Source)).ToArray(), ref Hash1, ref Hash2);
        RemoveUnnecessary(Directory.GetFiles(Replica).Union(Directory.GetDirectories(Replica)).ToArray());

        Console.WriteLine(Environment.NewLine + "Checkup complete at " + DateTime.Now.ToString());
        Console.WriteLine("Next checkup at " + DateTime.Now.AddMilliseconds(60000).ToString() + Environment.NewLine);
        Logger.Instance.Log(Environment.NewLine + "Checkup complete at " + DateTime.Now.ToString());
        Logger.Instance.Log("Next checkup at " + DateTime.Now.AddMilliseconds(60000).ToString() + Environment.NewLine);

        if (FailedFiles.Count > 0)
        {
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine("Failed to update:");
          foreach (var file in FailedFiles)
          {
            Console.WriteLine(file);
          }
          Console.ForegroundColor = ConsoleColor.White;
        }
        if (FailedToRemoveFiles.Count > 0)
        {
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine("Failed to remove:");
          foreach (var file in FailedToRemoveFiles)
          {
            Console.WriteLine(file);
          }
          Console.WriteLine("Closing down any programs that may use it should help");
          Console.ForegroundColor = ConsoleColor.White;
        }


        lock (_lock)
        {
          Monitor.Wait(_lock, TimePeriod);
        }
      }

      Environment.Exit(0);
    }

    //Function that will create missing files, folders
    //and update files that differs from source
    static void Synchronisation(string[] sourceFileNames, ref Span<byte> hash1, ref Span<byte> hash2)
    {
      //Sychronizing the existing files from source folder
      //to replica folder
      if (sourceFileNames.Length > 0)
      {
        foreach (string SourceFilePath in sourceFileNames)
        {
          //Reccurency for folders within source
          if (Directory.Exists(SourceFilePath))
          {
            //Check if folder exists in replica
            string AdjustedReplicaFolderPath = SourceFilePath.Replace(Source, Replica);
            if (!Directory.Exists(AdjustedReplicaFolderPath))
            {
              Directory.CreateDirectory(AdjustedReplicaFolderPath);
            }
            //Synch the insides of this folder
            Synchronisation(Directory.GetFiles(SourceFilePath).Union(Directory.GetDirectories(SourceFilePath)).ToArray(), ref hash1, ref hash2);
          }
          else
          {
            string SourceFileName = Path.GetFileName(SourceFilePath);

            //Path adjusted in case of recurency
            string ReplicaFilePath = SourceFilePath.Replace(Source, Replica);

            Console.WriteLine("Checking: " + SourceFileName);
            Logger.Instance.Log("Checking: " + SourceFileName);

            //If the contents does not match or does not exist
            //correct the file in replica. Contents are checked
            //based on their checksum as it is faster than bit by bit
            if (File.Exists(ReplicaFilePath))
            {
              using var stream1 = new FileStream(SourceFilePath, FileMode.Open, FileAccess.Read);
              using var stream2 = new FileStream(ReplicaFilePath, FileMode.Open, FileAccess.Read);

              if (stream1.Length != stream2.Length)
              {
                stream1.Close();
                stream2.Close();
                if (ReplaceFile(SourceFilePath, ReplicaFilePath))
                {
                  Console.WriteLine(SourceFileName + " was outdated. Updated to the source version.");
                  Logger.Instance.Log(SourceFilePath + " was outdated. Updated to the source version.");
                  break;
                }
              }
              MD5.HashData(stream1, hash1);
              MD5.HashData(stream2, hash2);

              if (!hash1.SequenceEqual(hash2))
              {
                if (ReplaceFile(SourceFilePath, ReplicaFilePath))
                {
                  Console.WriteLine(SourceFileName + " was outdated. Updated to the source version.");
                  Logger.Instance.Log(SourceFilePath + " was outdated. Updated to the source version.");
                }
              }
              stream1.Close();
              stream2.Close();
            }
            else
            {
              if (ReplaceFile(SourceFilePath, ReplicaFilePath))
              {
                Console.WriteLine(SourceFileName + " was missing. Added to the destination.");
                Logger.Instance.Log(SourceFilePath + " was missing. Added to the destination.");
              }
            }

          }
        }
      }
      else
      {
        Console.WriteLine("\nError: There are no files in source direction.");
        Environment.Exit(0);
      }
    }

    //Function that will check for any unneccessary files and
    //folders in the replica folder
    static void RemoveUnnecessary(string[] replicaFileNames)
    {
      if (replicaFileNames.Length > 0)
      {
        foreach (string ReplicaFilePath in replicaFileNames)
        {
          //Path adjusted in case of recurency
          string AdjustedReplicaFilePath = ReplicaFilePath.Replace(Replica, "");

          //Removing unnecesary folders
          if (Directory.Exists(ReplicaFilePath))
          {
            if (!Directory.Exists(ReplicaFilePath.Replace(Replica, Source)))
            {
              Directory.Delete(ReplicaFilePath, true);
              Console.WriteLine("Removed unnecessary folder: " + ReplicaFilePath.Replace(Replica, ""));
              Logger.Instance.Log("Removed unnecessary folder: " + ReplicaFilePath);
            }
            //search deeper
            else
              RemoveUnnecessary(Directory.GetFiles(ReplicaFilePath).Union(Directory.GetDirectories(ReplicaFilePath)).ToArray());
          }
          //Removing unnecesary files
          else
          {
            if (!File.Exists(Source + AdjustedReplicaFilePath))
            {
              try
              {
                File.Delete(ReplicaFilePath);
                Console.WriteLine("Removed unnecessary file: " + Path.GetFileName(ReplicaFilePath).Replace(Replica, ""));
                Logger.Instance.Log("Removed unnecessary file: " + ReplicaFilePath);
              }
              catch (Exception)
              {
                FailedToRemoveFiles.Add(ReplicaFilePath);
                Console.WriteLine("Failed to remove " + Path.GetFileName(ReplicaFilePath));
                Logger.Instance.Log("Failed to remove " + Path.GetFileName(ReplicaFilePath));
              }
            }
          }
        }
      }
    }

    //function that replaces old file with new from the source
    static bool ReplaceFile(string SourceFilePath, string ReplicaFilePath)
    {
      try
      {
        File.Copy(SourceFilePath, ReplicaFilePath, true);
        return true;
      }
      catch (Exception ex)
      {
        FailedFiles.Add(ReplicaFilePath);
        Console.WriteLine(ex);
        Console.WriteLine("Failed to update " + Path.GetFileName(SourceFilePath));
        Logger.Instance.Log("Failed to update " + Path.GetFileName(SourceFilePath));
      }
      return false;
    }

  }

  public sealed class Logger
  {
    private static Logger instance = null;
    private static string LogFilePath;
    private Logger()
    {
      LogFilePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Logs" + DateTime.Now.ToString().Replace(":", "-") + ".txt";
    }

    public static Logger Instance
    {
      get
      {
        if (instance == null)
        {
          instance = new Logger();
        }
        return instance;
      }
    }

    public void Log(string Message)
    {
      File.AppendAllText(LogFilePath, Message + Environment.NewLine);
    }
  }

}