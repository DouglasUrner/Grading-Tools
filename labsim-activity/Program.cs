using System;

using Microsoft.VisualBasic.FileIO;

namespace labsim_activity;
class Program
{
    static void Main(string[] args)
    {
        var path = @"scores.csv";
        using (TextFieldParser csvParser = new TextFieldParser(path))
        {
          // Setup parser
          csvParser.CommentTokens = new string[] { "#" };
          csvParser.SetDelimiters(new string[] { "," });
          csvParser.HasFieldsEnclosedInQuotes = true;

          // Skip the row with the column names
          //csvParser.ReadLine();
          string[] resourceNames = csvParser.ReadFields();
          ResourceInfo[] resources = ResourceInfo.ParseResourceList(resourceNames);

          string[] times = csvParser.ReadFields();
          ConvertTimes(times, 1);

          while (!csvParser.EndOfData)
          {
            // Read current line fields, pointer moves to the next line.
            string[] fields = csvParser.ReadFields();
            string Name = fields[0];

            ConvertTimes(fields, 1);
            ScoreTimes(times, fields, 1, 10);

            Console.Write($"{Name}:");
            for (int i = 1; i < fields.Length; i++)
            {
              if (fields[i] != String.Empty) { Console.Write($" {fields[i]}"); }
            }
            Console.WriteLine();
          }
        }
    }

    ///
    // ScoreTimes -- assign a score based on the amount of time a student is expected to spend
    // engaged with the unit resources. Calculated as ActualTime / ExpectedTime * PossiblePoints.

    static void ScoreTimes(string[] expected, string[] actual, int offset, int possible)
    {
      int score = 0;

      for (int i = offset; i < expected.Length; i++)
      {
        if (expected[i] != String.Empty)
        {
          double expectedTime = Int32.Parse(expected[i]);
          if (actual[i] != String.Empty)
          {
            double actualTime = Int32.Parse(actual[i]);
            if (actualTime >= expectedTime)
            {
              score = possible;
            }
            else
            {
              score = (int) (actualTime / expectedTime * possible);
            }
            //actual[i] = $"{actualTime}/{expectedTime} ({score})";
          }
        }
        actual[i] = score.ToString();
      }
    }

    ///
    // Convert times returned by LabSim (in the form Mm Ss) to plain seconds.

    static void ConvertTimes(string[] t, int offset)
    {
      string min = String.Empty;
      string sec = String.Empty;

      for (int i = offset; i < t.Length; i++)
      {
        if (t[i] != String.Empty)
        {
          //Console.WriteLine(t[i]);
          var minLength = t[i].IndexOf('m');
          min = (minLength > 0) ? t[i].Substring(0, minLength) : "0";
          
          // Console.WriteLine(m);
          var secStart = t[i].IndexOf(' ') + 1;
          var secLength = t[i].IndexOf('s') - secStart;
          sec = (secStart > 0 && secLength > 0) ? t[i].Substring(secStart, secLength) : "0";
          // Console.WriteLine(s);
          // XXX TryParse would be better here...
          var seconds = Int32.Parse(min) * 60 + Int32.Parse(sec);
          //Console.WriteLine(seconds);
          t[i] = seconds.ToString();
        }
      }
    }
}

public class ResourceInfo
{
  public string type;
  public ResourceSection section;
  public string title;

  public ResourceInfo(string ty, ResourceSection s, string ti)
  {
    type = ty;
    section = s;
    string title = ti;
  }

  public static ResourceInfo[] ParseResourceList(string[] list)
  {
    ResourceInfo[] resources = new ResourceInfo[list.Length];

    resources[0] = null;

    for (int i = 1; i < list.Length; i++)
    {
      if (list[i] != String.Empty) { resources[i] = ParseResourceInfo(list[i]); }
    }

    return resources;
  }

  public static ResourceInfo ParseResourceInfo(string info)
  {
    Console.WriteLine(info);

    // Resource Type
    var dash = info.IndexOf('-');
    var type = info.Substring(0, dash - 1);
    Console.WriteLine(type);

    // Section Number
    var space = (info + dash).IndexOf(' ');
    Console.WriteLine(info + dash);
    Console.WriteLine(space);
    var sectionString = info.Substring(dash + 2, space);
    Console.WriteLine(sectionString);
    var section = ResourceSection.ParseResourceSection(sectionString);

    // Resource Title
    var title = info.Substring(dash + space + 1);
    Console.WriteLine(title);

    return new ResourceInfo(type, section, title);
  }

}

public class ResourceSection
{
  public string chapter;
  public string lesson;
  public string item;

  public ResourceSection(string c, string l, string i)
  {
    chapter = c;
    lesson = l;
    item = i;
  }

  public static ResourceSection ParseResourceSection(string s)
  {
    var c = s.Substring(0, s.IndexOf('.'));
    var l = s.Substring(c.Length + 1, s.IndexOf('.'));
    var i = s.Substring(c.Length + l.Length + 2);

    return new ResourceSection(c, l, i);
  }
}
