makecert -n "CN=Cowboy" -a "SHA1" -pe -r -sv RootCowboy.pvk RootCowboy.cer
makecert -n "CN=Cowboy" -a "SHA1" -pe -ic RootCowboy.cer -iv RootCowboy.pvk -sv Cowboy.pvk -sky Exchange Cowboy.cer
cert2spc Cowboy.cer Cowboy.spc
pvkimprt -pfx Cowboy.spc Cowboy.pvk


PVK Digital Certificate Files Importer
https://www.microsoft.com/en-us/download/details.aspx?id=6563
