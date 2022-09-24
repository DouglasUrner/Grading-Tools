namespace Template
{
  using System;

  using FluentArgs;

  public class Program
  {
    public static void Main(string[] args)
    {
      FluentArgsBuilder.New()
        .DefaultConfigs()
        .Parameter<uint>("--start-on")
          .IsOptionalWithDefault(7)
        .Parameter("--student-list").IsRequired()
        .Flag("--verbose", "-v")
        .Call(verbose => studentList => startOn =>
          {
            if (verbose) Console.WriteLine($"Skipping first {startOn - 1} records");
            //Process(studentList);
            Console.WriteLine($"Process {studentList}");
          }
        )
        .Parse(args);        
    }
    void Process(string studentList)
    {
      Console.WriteLine($"Process {studentList}");
    }
  }
}