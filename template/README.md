# Grading Tools Template

* Read student information from an export from the SIS (CSV from Skyward for starters).
* Write a file suitable for importing grades into the SIS.
* Fuzzy check for the existance of a directory.
* Fuzzy check for the existance of a file.
* Copy a file from a student machine.
* Invoke a process (e.g., run tests) for each student.

## Packages Used

* **FluentArgs**: argument parsing ([kutoga/FluentArgs][fa]).
* **CsvHelper**: read & write CSV files ([JoshClose/CsvHelper][csv]).

[fa]: <https://github.com/kutoga/FluentArgs>
[cvs]: <https://github.com/joshclose/CsvHelper/>