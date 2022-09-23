using System;
using System.Collections;
using System.IO;

using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

public class BackupChecker
{
    /*
     * Check for:
     *      - The existance of a folder that exactly matches backupFolder: +4 points
     *      - The existance of a folder that matches the tightBackupFolderRegex: +3 points
     *      - The existance of a folder that matches the looseBackupFolderRegex: +2 points
     *      - The existance of a folder created on backupDate that contains a .unitypackage file: 1 point
     *      - A file in a backup folder that matches the backupName: +4 points
     *      - A file in the backup folder that matches the tightBackupNameRegex: +3 points
     *      - A file in the backup folder that matches the looseBackupNameRegex: +2 points
     *      - A .unitypackage file in the backup folder that was created on backupDate: +1 point
     */

    // Defaults for command line arguments:
    static string studentRoot = "\\\\skhs04\\stusers\\";
    static string backupFolder = "Unity Project Backups";
    static string tightBackupFolderRegex = "Unity Project[s]* Backup[s]*";
    static string looseBackupFolderRegex = "backup";
    static string projectName = "Prototype-1";
    static string tightBackupFileRegex = "Prototype[-_ .]*1[-_ .]2022-09-2[012].unitypackage";
    static string backupDate = "2022-09-22";
    string backupName = projectName + "-" + backupDate;

    static bool verbose = false;

    static void Main(string[] args)
    {
        /*
         * Process command line arguments.
         */
        // var verboseOption = new Option<bool>(
        //     name: "-v",
        //     description: "Turn on process tracing."
        //     );

        int headerLength = 7;
        int emailCol = 4;

        string studentHome;

        foreach (string arg in args)
        {
            if (verbose) Console.WriteLine("Processing: " + arg);
            int record = 0;

            using (TextFieldParser parser = new TextFieldParser(arg))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    int score = 0;

                    string[] fields = parser.ReadFields();
                    if (record++ >= headerLength)
                    {
                        var n = fields[emailCol].IndexOf("@");
                        var userName = fields[emailCol].Substring(0, n);
                        studentHome = studentRoot + userName;
                        if (verbose && !string.IsNullOrEmpty(userName)) Console.WriteLine("    Checking: " + userName + " in: " + studentHome);

                        var dir = FindBackupDir(studentHome);
                        if (dir.path != null)
                        {
                            if (verbose) Console.WriteLine("  Found: '" + dir.path + "' points: " + dir.points);
                            score += dir.points;
                            var backup = FindBackupFile(dir.path, projectName, backupDate);
                            if (backup.path != null)
                            {
                                score += backup.points + 8;
                                Console.WriteLine((record - headerLength) + ": " + userName + ": " + score + ": " + backup.path);
                            }
                            else
                            {
                                Console.WriteLine((record - headerLength) + ": " + userName + ": " + score + ": Backup file not found in '" + dir.path + "'");
                            }
                        }
                        else
                        {
                            Console.WriteLine((record - headerLength) + ": " + userName + ": 0:" + "Backup directory not found in '" + studentHome + "'");
                        }
                    }
                }
            }
        }
    }

    static PathAndPoints FindBackupDir(string homeDir)
    {
        // Check for requested path.
        if (Directory.Exists(homeDir))
        {
            var path = homeDir + "\\" + backupFolder;

            if (verbose) Console.WriteLine("        Looking for: " + path);
            if (Directory.Exists(path))
            {
                // Exact match.
                if (verbose) Console.WriteLine("    Found exact match: '" + path + "'");
                return new PathAndPoints(path, 4);
            }
            else
            {
                // Check for inexact matches
                if (verbose) Console.WriteLine("          Checking inexact matches:");
                DirectoryInfo di = new DirectoryInfo(homeDir);

                foreach (var dir in di.EnumerateDirectories())
                {
                    if (verbose) Console.Write("          " + dir.FullName);

                    // Tight match (3 points).
                    Match tm = Regex.Match(dir.Name, tightBackupFolderRegex, RegexOptions.IgnoreCase);
                    if (tm.Success)
                    {
                        if (verbose) Console.WriteLine(": tight match");
                        return new PathAndPoints(dir.FullName, 3);
                    }

                    // Loose match (2 points).
                    Match lm = Regex.Match(dir.Name, looseBackupFolderRegex, RegexOptions.IgnoreCase);
                    if (lm.Success)
                    {
                        if (verbose) Console.WriteLine(": loose match");
                        return new PathAndPoints(dir.FullName, 2);
                    }

                    // Minimal match -- perhaps there is a .unitypackage file in this directory. If there
                    // is, return the path to the directory -- FindBackupFile will be called again to get
                    // file name points.
                    if (verbose) Console.WriteLine(": checking for minimal match");
                    var mm = FindBackupFile(dir.FullName, projectName, backupDate);
                    if (mm.path != null) return new PathAndPoints(dir.FullName, 1);

                }
                // Last possiblity in the root of the home directory
                var rd = FindBackupFile(homeDir, projectName, backupDate);
                if (rd.path != null) return new PathAndPoints(homeDir, 1);

                return new PathAndPoints(null, 0);
            }
        }
        else
        {
            return new PathAndPoints(null, 0);
        }
    }

    static PathAndPoints FindBackupFile(string backupDir, string project, string date)
    {
        var backupFileName = project + "-" + date + ".unitypackage";

        if (File.Exists(backupDir + "\\" + backupFileName))
        {
            if (verbose) Console.WriteLine("         Exact match: " + backupFileName);
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

                Match tm = Regex.Match(fi.Name, tightBackupFileRegex, RegexOptions.IgnoreCase);
                if (tm.Success)
                {
                    if (verbose) Console.WriteLine("         Tight match: " + fi.FullName);
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
                    if (verbose) Console.WriteLine("         Minimal match: " + fi.FullName);
                    return new PathAndPoints(fi.Name, 1);
                }
            }
            return new PathAndPoints(null, 0);
        }
    }
}

public class PathAndPoints
{
    public string? path { get; set; }
    public int points { get; set; }

    public PathAndPoints(string? path, int points)
    {
        this.path = path;
        this.points = points;
    }
}