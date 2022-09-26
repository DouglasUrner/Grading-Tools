namespace Check_Backups
{
  using System;
  using System.Globalization;
  using System.IO;
  using System.Text.RegularExpressions;

  using CsvHelper;
  using CsvHelper.Configuration;
  using FluentArgs;

  public class Program
  {
    // Command line options
    static Options opts = new Options();

    public static void Main(string[] args)
    {
      FluentArgsBuilder.New()
        .DefaultConfigs()
        .Parameter("--roster")
          .WithDescription("CSV file holding roster information.")
          .IsRequired()
        .Parameter("--output")
          .WithDescription("Where to write results as CSVs.")
          .WithExamples("S1A1-U1L1-scores.csv")
          .IsRequired()
        .Parameter("--due")
          .WithDescription("The ISO date on which the assignment is due, defaults to today.")
          .WithExamples("YYYY-MM-DD, 2001-09-22")
          .IsOptionalWithDefault(Options.DefaultDueDate())
        .Parameter<uint>("--skip")
          .WithDescription("# of records to skip in student list.")
          .IsOptionalWithDefault(8)
        .Parameter<uint>("--name")
          .WithDescription("Column holding student names (as last, first mi)")
          .IsOptionalWithDefault(1)
        .Parameter<uint>("--email")
          .WithDescription("Column holding student email addresses")
          .IsOptionalWithDefault(2)
        .Flag("--verbose", "-v")
          .WithDescription("Provide detailed process trace.")
        .Call(verbose => email => name => skip => due => output => roster =>
          {
            opts.Verbose = verbose;
            opts.DueDate = due;

            Process(roster, output, skip, name, email);
          }
        )
        .Parse(args);        
    }
    static void Process(string roster, string output, uint skip, uint name, uint email)
    {
      if (opts.Verbose) Console.WriteLine($"Skipping first {skip} records");
      Console.WriteLine($"Process {roster}");

      var config = new CsvConfiguration(CultureInfo.InvariantCulture)
      {
        HasHeaderRecord = false,
      };

      try // Open roster and skip header.
      {
        using (var sr = new StreamReader(roster))
        {
          // Skip the file header.
          while ((sr.ReadLine()) != null)
          {
            if (--skip > 1)
            {
              continue;
            }
            else
            {
              break;
            }
          }
        
          using (var csv = new CsvReader(sr, config))
          {
            while (csv.Read())
            {
              var ri = csv.GetRecord<RosterInfo>();
              PathAndPoints pnp;

              /*
              ** Split FullName into first and last names.
              */
              ri.LastName = RosterInfo.InitializeLastName(ri);
              ri.FirstName = RosterInfo.InitializeFirstName(ri);

              /*
              ** Extract user name from e-mail address.
              */
              ri.Username = RosterInfo.InitializeUsername(ri);

              pnp = CheckBackup(ri.Username);
              ShowScore(ri, pnp);
            }
          }
        }
      }
      catch (System.Exception e)
      {
        // Let the user know what went wrong.
        Console.WriteLine("The file could not be read:");
        Console.WriteLine(e.Message);
        //throw;
      }
    }

    static PathAndPoints CheckBackup(string user)
    {
      var bd = new PathAndPoints();
      var bf = new PathAndPoints();

      var homeDir = $"{opts.NetworkHome}\\{user}";
      if (opts.Verbose) Console.WriteLine($"Check {homeDir}");
      bd = FindBackupDir(homeDir);
      if (bd.path == null)
      {
        return bd;
      }
      else
      {
        bd = FindBackupFile(bd.path);
        if (bf.path == null)
        {
          var msg = $"No project backup found in {bd.path}";
          return new PathAndPoints(null, bd.points, msg);
        }
        else
        {
         return bf;
        }
      }
    }

    static void ShowScore(RosterInfo ri, PathAndPoints pnp)
    {
      Console.WriteLine($"{ri.FirstName} {ri.LastName}: {pnp.points}: {pnp.path}: {pnp.msg}");
    }
    
    static PathAndPoints FindBackupDir(string homeDir)
    {
      var backupFolder = opts.BackupDir;

      // Check for requested path.
      if (Directory.Exists(homeDir))
      {
          var path = homeDir + "\\" + backupFolder;

          if (opts.Verbose) Console.WriteLine("        Looking for: " + path);
          if (Directory.Exists(path))
          {
            // Exact match.
            if (opts.Verbose) Console.WriteLine("    Found exact match: '" + path + "'");
            return new PathAndPoints(path, 4);
          }
          else
          {
            // Check for inexact matches
            if (opts.Verbose) Console.WriteLine("          Checking inexact matches:");
            DirectoryInfo di = new DirectoryInfo(homeDir);

            foreach (var dir in di.EnumerateDirectories())
            {
              if (opts.Verbose) Console.Write("          " + dir.FullName);

              // Tight match (3 points).
              Match tm = Regex.Match(dir.Name, opts.BackupDirTightRegex, RegexOptions.IgnoreCase);
              if (tm.Success)
              {
                if (opts.Verbose) Console.WriteLine(": tight match");
                return new PathAndPoints(dir.FullName, 3);
              }

              // Loose match (2 points).
              Match lm = Regex.Match(dir.Name, opts.BackupDirLooseRegex, RegexOptions.IgnoreCase);
              if (lm.Success)
              {
                if (opts.Verbose) Console.WriteLine(": loose match");
                return new PathAndPoints(dir.FullName, 2);
              }

              // Minimal match -- perhaps there is a .unitypackage file in this directory. If there
              // is, return the path to the directory -- FindBackupFile will be called again to get
              // file name points.
              if (opts.Verbose) Console.WriteLine(": checking for minimal match");
              var mm = FindBackupFile(dir.FullName, opts.ProjectName, opts.DueDate);
              if (mm.path != null) return new PathAndPoints(dir.FullName, 1);

            }
            // Last possiblity in the root of the home directory
            var rd = FindBackupFile(homeDir, opts.ProjectName, opts.DueDate);
            if (rd.path != null) return new PathAndPoints(homeDir, 1);

            return new PathAndPoints(null, 0);
        }
      }
      else
      {
        return new PathAndPoints(null, 0);
      }
    }

    static PathAndPoints FindBackupFile(string backupDir)
    {
      return FindBackupFile(backupDir, opts.ProjectName, opts.DueDate);
    }
    
    static PathAndPoints FindBackupFile(string backupDir, string project, string date)
    {
      var backupFileName = project + "-" + date + ".unitypackage";

      if (File.Exists(backupDir + "\\" + backupFileName))
      {
        if (opts.Verbose) Console.WriteLine("         Exact match: " + backupFileName);
        return new PathAndPoints(backupFileName, 4);
      }
      else
      {
        DirectoryInfo di = new DirectoryInfo(backupDir);

        foreach (var fi in di.EnumerateFiles())
        {
          /*
           * Tight match
           */

          var regex = opts.ProjectNameTightRegex + opts.DueDate;

          Match tm = Regex.Match(fi.Name, regex, RegexOptions.IgnoreCase);
          if (tm.Success)
          {
            if (opts.Verbose) Console.WriteLine("         Tight match: " + fi.FullName);
            return new PathAndPoints(fi.Name, 3);
          }

          /*
           * Loose match
           */

          /*
           * Minimal match
           */

          Match mm = Regex.Match(fi.Name, ".unitypackage");
          if (mm.Success)
          {
            if (opts.Verbose) Console.WriteLine("         Minimal match: " + fi.FullName);
            return new PathAndPoints(fi.Name, 1);
          }
        }
        return new PathAndPoints(null, 0);
      }
    }
  }
}

public class RosterInfo
{
  // XXX - fix these so they are not-nullable.
  public string FullName { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;
  public string LastName { get; set; } = string.Empty;
  public string FirstName { get; set; } = string.Empty;
  public string Username { get; set; } = string.Empty;
  public string NetworkHome { get; set; } = string.Empty;
  public string LocalHome { get; set; } = string.Empty;

  public static string InitializeFirstName(RosterInfo ri)
  {
    var fn = ri.FullName.Trim();
    var beg = fn.IndexOf(",") + 2;
    int end;

    if (fn[fn.Length - 1] == '.')
    {
      end = fn.Length - (beg + 2);
    }
    else
    {
      end = fn.Length - beg;
    }

    return fn.Substring(beg, end).Trim();
  }

  public static string InitializeLastName(RosterInfo ri)
  {
    return ri.FullName.Substring(0, ri.FullName.IndexOf(",")).Trim();
  }

  public static string InitializeUsername(RosterInfo ri)
  {
    return ri.Email.Substring(0, ri.Email.IndexOf("@"));
  }
}

public class PathAndPoints
{
  public string? path { get; set; } = null;
  public int points { get; set; }

  public string msg { get; set; } = string.Empty;

  public PathAndPoints(string? path, int points, string msg)
  {
    this.path = path;
    this.points = points;
    this.msg = msg;
  }

  public PathAndPoints(string? path, int points)
  {
    this.path = path;
    this.points = points;
    this.msg = string.Empty;
  }

  public PathAndPoints()
  {
    this.path = null;
    this.points = 0;
    this.msg = string.Empty;
  }
}

public class Options
{
  public string BackupDir { get; set; } = "Unity Project Backups";
  public string BackupDirLooseRegex = "backup";
  public string BackupDirTightRegex = "Unity Project[s]* Backup[s]*";
  public string DueDate { get; set; } = string.Empty;
  public string NetworkHome { get; set; } = "\\\\skhs02\\stusers";
  public string ProjectName { get; set; } = "Prototype-1";
  public string ProjectNameTightRegex { get; set; } = "Prototype[-_ .]*1[-_ .]";
  public bool Verbose { get; set; }

  public static string DefaultDueDate()
  {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var due = DateTime.Now.ToString("s", ci);
      var n = due.IndexOf("T");
      
      return due.Substring(0, n);
  }
}