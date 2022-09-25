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
    static Options opts;

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
        .Call(verbose => email => name => skip => output => roster =>
          {
            opts = new Options(verbose);
            Process(roster, output, skip, name, email);
          }
        )
        .Parse(args);        
    }
    static void Process(string roster, string output, uint skip, uint name, uint email)
    {
      if (opts.verbose) Console.WriteLine($"Skipping first {skip} records");
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
              var record = csv.GetRecord<RosterInfo>();

              // Split apart FullName into first and last names.
              var full = record.FullName;
              var n = full.IndexOf(",");

              record.LastName = full.Substring(0, n).Trim();

              var first = full.Substring(n + 2, full.Length - (n + 2)).Trim();
              if (first[first.Length - 1] == '.')
              {
                first = first.Substring(0, first.Length - 3);
                //Console.WriteLine($"first.Length: {first.Length}");
              }
              record.FirstName = first;

              Console.WriteLine($"{record.FullName}: {record.FirstName} {record.LastName}");
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
  public string FullName { get; set; }
  public string Email { get; set; }
  public string LastName { get; set; }
  public string FirstName { get; set; }
  public string NetworkHome { get; set; }
  public string LocalHome { get; set; }
}

public class Options
{
  public bool verbose { get; set; }

  public Options(bool verbose)
  {
    this.verbose = verbose;
  }
}