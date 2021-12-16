# MySQL_ToTSQLConverter
This is a program to convert MySQL Stored Procedures (and table creation) to MS SQL TSQL Stored Procedures

This program was written a decade ago when I had the task of converting 75 stored procedures over to TSQL. 
At the time, there weren't any such conversion programs. The only conversions I could find were just of the table definitions.

I got this program to the point where it did most of the simple work of converting, some of the formatting, how the if statements
needed to be different, etc. It made the remaining manual process much smaller.

This program does not do everything someone non-technical would want it to do. It does not properly convert all the built in function arguments.
Nor does it properly handle calling subroutines (other stored procedures).

Basically, it is not a "finished product". 
I stopped when further work wasn't going to help get the project at hand further. I got the job done.

As it was written quite some time ago, it handled MySQL version 5.5.

