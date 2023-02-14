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
        .Parameter("--root")
          .WithDescription("Root directory for student homes.")
          .IsOptionalWithDefault(opts.Root)
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
        .Call(verbose => email => name => skip => due => output => roster => root =>
          {
            opts.Verbose = verbose;
            opts.DueDate = due;
            opts.Root = root;

            Process(roster, output, skip, name, email);
          }
        )
        .Parse(args);        
    }

    static void Process(string roster, string output, uint skip, uint name, uint email)
    {
      if (opts.Verbose) Console.WriteLine($"Process {roster}");
      if (opts.Verbose) Console.WriteLine($"Skipping first {skip} records");

      var config = new CsvConfiguration(CultureInfo.InvariantCulture)
      {
        HasHeaderRecord = false,
      };

      try // Open roster and skip class information.
      {
        using (var sr = new StreamReader(roster))
        {
          // Skip the file skit class information.
          while (skip > 0 && (sr.ReadLine()) != null)
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
              // PathAndPoints pnp;

              if (opts.Verbose) Console.WriteLine($"Processing {ri}");

              /*
              ** Split FullName into first and last names.
              */
              ri.LastName = RosterInfo.InitializeLastName(ri);
              ri.FirstName = RosterInfo.InitializeFirstName(ri);

              /*
              ** Extract user name from e-mail address.
              */
              ri.Username = RosterInfo.InitializeUsername(ri);

              Console.WriteLine($"{ri.FirstName} {ri.LastName} ({ri.Username}):");

              var homeDir = $"{opts.Root}{Path.DirectorySeparatorChar}{ri.Username}";
              var bd = FindBackupDir(homeDir);
              if (bd != null)
              {
                Console.WriteLine($"\tBackup Directory: {bd.ToString()}");
                var bf = FindBackupFile(bd.path, opts.ProjectName, opts.DueDate);
                Console.WriteLine($"\tExported Assets:  {bf.ToString()}");
                Console.WriteLine($"\tTotal Points:     {bd.points + bf.points}");
              }

              //pnp = CheckBackup(ri.Username);
              //ShowScore(ri, pnp);
            }
          }
        }
      }
      catch (System.Exception e)
      {
        // Let the user know what went wrong.
        Console.WriteLine(e.ToString());
        //throw;
      }
    }

    static PathAndPoints CheckBackup(string user)
    {
      var bd = new PathAndPoints();
      var bf = new PathAndPoints();

      var homeDir = $"{opts.Root}{Path.DirectorySeparatorChar}{user}";
      if (opts.Verbose) Console.WriteLine($"Check {homeDir}");
      bd = FindBackupDir(homeDir);
      if (bd.path == null)
      {
        bd.msg = $"No backup or backup directory found in {homeDir}";
        return bd;
      }
      else
      {
        // Found a reasonable candidate for the backup directory.
        bf = FindBackupFile(bd.path);
        if (bf.path == null)
        {
          // Didn't find a backup.
          var msg = $"No project backup found in {bd.path}";
          return new PathAndPoints(null, bd.points, msg);
        }
        else
        {
          // Found a backup.
          var msg = string.Empty;
          return new PathAndPoints(bf.path, 8 + bd.points + bf.points, msg, bf.created);
        }
      }
    }

    static void ShowScore(RosterInfo ri, PathAndPoints pnp)
    {
      if (pnp.path != null)
      {
        Console.WriteLine($"{ri.FirstName} {ri.LastName}: {pnp.points}: {pnp.path} ({pnp.created.ToString()}): {pnp.msg}");
      }
      else
      {
        Console.WriteLine($"{ri.FirstName} {ri.LastName}: {pnp.points}: {pnp.msg}");
      }
    }
    
    static PathAndPoints FindBackupDir(string homeDir)
    {
      var backupDir = opts.BackupDir;

      // Check for requested path.
      if (Directory.Exists(homeDir))
      {
          var path = homeDir + Path.DirectorySeparatorChar + backupDir;

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
            // Last possiblity in the root of the home directory -- note that we don't 
            // return the backup that we found, we are just setting the directory to look
            // in when FindBackupFile() is called after we return.
            var rd = FindBackupFile(homeDir, opts.ProjectName, opts.DueDate);
            if (rd.path != null) return new PathAndPoints(homeDir, 1);

            return new PathAndPoints(null, 0);
        }
      }
      else
      {
        return new PathAndPoints(null, 0, $"Network home directory '{homeDir}' not found.");
      }
    }

    static PathAndPoints FindBackupFile(string backupDir)
    {
      return FindBackupFile(backupDir, opts.ProjectName, opts.DueDate);
    }
    
    static PathAndPoints FindBackupFile(string? backupDir, string project, string date)
    {
      int points = 0;
      string? path = null;

      if (backupDir == null) { return new PathAndPoints(null, 0, "No backup directory to search"); }

      // Regex for an exact match:
      string exactRegex = $"{opts.ProjectName}-2023-02-09.unitypackage$"; // XXX!!!
      // Regex for properly constructed backup file name:
      string backupRegex = "^.*_[0-9]{4}-[0-9]{2}-[0-9]{2}(?:_.*)?.unitypackage$";

      DirectoryInfo di = new DirectoryInfo(backupDir);

      foreach (var fi in di.EnumerateFiles())
      {
        Match m = Regex.Match(fi.Name, exactRegex);
        if (m.Success)
        {
          if (opts.Verbose) Console.WriteLine($"{fi.FullName}: exact: ({fi.CreationTime})");
          path = fi.FullName;
          points = 4;
          return new PathAndPoints(path, points);
        }
        // Check for backups with well formed names.
        m = Regex.Match(fi.Name, backupRegex, RegexOptions.IgnoreCase);
        if (m.Success)
        {
          path = fi.FullName;
          points = 2;
          if (opts.Verbose) Console.WriteLine($"{fi.FullName}: correct pattern: ({fi.CreationTime})");
          break;
        }

        // Check for any .unityproject file.
        m = Regex.Match(fi.Name, "\\.unitypackage$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
          path = fi.FullName;
          points = 1;
          if (opts.Verbose) Console.WriteLine($"{fi.FullName}: possible backup: ({fi.CreationTime})");
          break;
        }
      }
      return new PathAndPoints(path, points);

      // var backupFileName = project + "_" + date + ".unitypackage";

      // if (opts.Verbose) Console.Write("Check for exact match: ");
      // if (File.Exists(backupDir + Path.DirectorySeparatorChar + backupFileName))
      // {
      //   DateTime created = File.GetCreationTime(backupFileName);
      //   if (opts.Verbose) Console.WriteLine("found: '" + backupFileName + "'");
      //   return new PathAndPoints(backupDir + Path.DirectorySeparatorChar + backupFileName, 4, string.Empty, created);
      // }
      // else
      // {
      //   DirectoryInfo di = new DirectoryInfo(backupDir);

      //   foreach (var fi in di.EnumerateFiles())
      //   {
      //     /*
      //     ** Exact match with annotation.
      //     */
      //     var emaRegex = project + "_" + date + "_.*"; // XXX

      //     Match ema = Regex.Match(fi.Name, emaRegex, RegexOptions.IgnoreCase);
      //     if (ema.Success)
      //     {
      //       if (opts.Verbose) Console.WriteLine("found (with annotation): " + fi.FullName);
      //       return new PathAndPoints(backupDir + Path.DirectorySeparatorChar + fi.Name, 4, "", fi.CreationTime);
      //     }
      //     else
      //     {
      //       if (opts.Verbose) Console.WriteLine("not found");
      //     }

      //     /*
      //      * Tight match
      //      */

      //     var tmRegex = opts.ProjectNameTightRegex + opts.DueDate;

      //     Match tm = Regex.Match(fi.Name, tmRegex, RegexOptions.IgnoreCase);
      //     if (tm.Success)
      //     {
      //       if (opts.Verbose) Console.WriteLine("         Tight match: " + fi.FullName);
      //       return new PathAndPoints(backupDir + Path.DirectorySeparatorChar + fi.Name, 3, "", fi.CreationTime);
      //     }

      //     /*
      //      * Loose match
      //      */

      //     /*
      //      * Minimal match
      //      */

      //     Match mm = Regex.Match(fi.Name, ".unitypackage");
      //     if (mm.Success)
      //     {
      //       if (opts.Verbose) Console.WriteLine("         Minimal match: " + fi.FullName);
      //       return new PathAndPoints(backupDir + Path.DirectorySeparatorChar + fi.Name, 1, "", fi.CreationTime);
      //     }
      //   }
      //   return new PathAndPoints(null, 0);
      //}
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
  public string Root { get; set; } = string.Empty;
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
    // Console.WriteLine($"{ri.FullName}: {ri.Email}");
    return ri.Email.Substring(0, ri.Email.IndexOf("@"));
  }
}

public class PathAndPoints
{
  public string? path { get; set; } = null;
  public int points { get; set; }
  public string msg { get; set; } = string.Empty;
  public DateTime created { get; set; }

  public PathAndPoints(string? path, int points, string msg, DateTime created)
  {
    this.path = path;
    this.points = points;
    this.msg = msg;
    this.created = created;
    //Console.WriteLine($"'{this.path}' Points: {this.points} Created: {this.created}");
  }

  public PathAndPoints(string? path, int points, string msg)
    : this(path, points, msg, new DateTime(0))
  {}

  public PathAndPoints(string? path, int points)
    : this(path, points, string.Empty, new DateTime(0))
  {}

  public PathAndPoints()
    : this (null, 0, string.Empty, new DateTime(0))
  {}

  public override string ToString()
  {
    string str = $"{this.points}";

    if (this.path == null) { str += $": {this.msg}"; }
    else if (this.msg != null) { str += $": {this.path}: {this.msg}"; }
    else { str += $": {this.path}"; }

    return str;
  }
}

public class Options
{
  public string BackupDir { get; set; } = "Unity Project Backups";
  public string BackupDirLooseRegex = "backup";
  public string BackupDirTightRegex = "Unity Project Backups";
  public string DueDate { get; set; } = string.Empty;
  public string Root { get; set; } = $"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}skhs04{Path.DirectorySeparatorChar}Stusers";
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