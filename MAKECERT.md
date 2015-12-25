Generate Certificates
------------
- makecert -n "CN=Cowboy" -a "SHA1" -pe -r -sv RootCowboy.pvk RootCowboy.cer
- makecert -n "CN=Cowboy" -a "SHA1" -pe -ic RootCowboy.cer -iv RootCowboy.pvk -sv Cowboy.pvk -sky Exchange Cowboy.cer
- cert2spc Cowboy.cer Cowboy.spc
- pvkimprt -pfx Cowboy.spc Cowboy.pvk

Import Certificates
------------
- Open Windows Certificate Manager by run **"certmgr.msc"**.
- Import **"RootCowboy.cer"** into your Computer store's Trusted Root Certification Authorities (on both the server and client). Notice that the certificate is issued to **"Cowboy"**. This must match the server name that the client expects: sslStream.AuthenticateAsClient(targetHost), where "targetHost" is the value of **"Cowboy"**.
- When your client connects, the server presents a certificate that tells the client **"I'm Cowboy"**. The client will accept this claim if the client machine trusts the CA that issued the certificate, which is achieved by importing **"RootCowboy.cer"** into the client's Trusted Root Certification Authorities.
- Finally, you need to import the private key that the server is going to use into the server machine's Personal store. This step is important because it addresses the server mode SSL must use a certificate with the associated private key. This is achieved by importing the **"Cowboy.pfx"** file that you generated earlier. Make sure that you change the file type filter to "all files" so that you can see the **"Cowboy.pfx"** file that you generated.

Help Links
------------
- [Download PVK Digital Certificate Files Importer](https://www.microsoft.com/en-us/download/details.aspx?id=6563)
- [SSLStream example - how do I get certificates that work?](http://stackoverflow.com/questions/9982865/sslstream-example-how-do-i-get-certificates-that-work)
- [The MakeCert tool creates an X.509 certificate](https://msdn.microsoft.com/library/windows/desktop/aa386968.aspx)
- [Working with Certificates](https://msdn.microsoft.com/en-us/library/ms731899(v=vs.110).aspx)
