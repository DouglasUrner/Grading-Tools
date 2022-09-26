namespace Template
{
  using System;
  using System.Globalization;
  using System.IO;

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

              /*
              ** Split FullName into first and last names.
              */
              ri.LastName = RosterInfo.InitializeLastName(ri);
              ri.FirstName = RosterInfo.InitializeFirstName(ri);

              /*
              ** Extract user name from e-mail address.
              */
              ri.Username = RosterInfo.InitializeUsername(ri);

              Console.WriteLine($"{opts.NetworkHome}\\{ri.Username}\\{opts.DueDate}");
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

public class Options
{
  public string DueDate { get; set; } = string.Empty;
  public string NetworkHome { get; set; } = "\\\\skhs02\\stusers";
  public bool Verbose { get; set; }

  public static string DefaultDueDate()
  {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var due = DateTime.Now.ToString("s", ci);
      var n = due.IndexOf("T");
      
      return due.Substring(0, n);
  }
}