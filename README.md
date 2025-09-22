Program that synchronizes two folders: source and replica. This will happen in intervals that can be changed. Logging feature was also implemented.

You can add arguments to the program as follows:

program.exe "path_to_source" "path_of_replica"

or

program.exe "path_to_source" "path_of_replica" interval_in_miliseconds

or

program.exe "path_to_source" "path_of_replica" interval_in_miliseconds "path_to_save_logs"

If no arguments are provided, the program will try to find "Source" and "Replica" folders in its own directory.

If path for logging was not provided it will create a log file in program's directory.

Program can be shutdown with Ctrl+C.
