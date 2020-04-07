
# Extra tools for Manager.io's database.

* Convert payments/receipts into transfers in manager.io
* Search payments/receipts across different accounts.


## Compile 

* Use monodevelop/xamarin studio.  Also copy the Manager.dll, protobuf-net.dll from the manager.io installation.

## To use...

* Quit manager.io, find where your .manager file is.
* Type 'bin/Debug/ManagerIO_Sqlite.exe ~/.local/share/Manager/xxx.manager'
* Visit http://localhost:8080 with a browser.
* Press a key when you're done.
* To change the default port of 8080, use --port=9999


## To update .tt files.

* Touch all files that include the .tt file.  Say aaa.tt, bbb.tt includes ccc.tt.  Run touch aaa.ttt bbb.tt to update them too.  This is a problem with monodevelop.
