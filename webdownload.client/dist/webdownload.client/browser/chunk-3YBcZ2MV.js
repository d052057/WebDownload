import{A as OE,B as Sv,Ft as ym,K as Wf,P as Qa,Pt as xf,R as RE,V as Tu,Y as Xa,_ as Ff,at as bf,bt as lv,dt as fv,gt as jv,h as FD,ht as jE,k as Nf,kt as qo,m as Ep,ot as cy,tt as _u}from"./main-O7EJ7OAF.js";import{s as cn}from"./chunk-4BN7ytnn.js";function O(r,o){if(r&1&&xf(0,`div`,23),r&2){let e=fv().$implicit;Sv(`alert alert-`+e.alert.type+` d-flex align-items-center mb-3`),Nf(`innerHTML`,e.alert.message,ym)}}function I(r,o){if(r&1){let e=lv();qo(0,`div`,24)(1,`div`,25)(2,`button`,26),Ff(`click`,function(){let m=Tu(e).$implicit;return _u(fv(2).copyCode(m.code,m.id))}),xf(3,`i`,27),jv(4),Qa(),qo(5,`pre`,11)(6,`code`),jv(7),Qa()()()()}if(r&2){let e=o.$implicit,c=fv(2);cy(3),Nf(`ngClass`,c.copiedId===e.id?`bi-check`:`bi-clipboard`),cy(),Xa(` `,c.copiedId===e.id?`Copied!`:`Copy`,` `),cy(3),Wf(e.code)}}function D(r,o){if(r&1&&(qo(0,`li`),jv(1),Qa()),r&2){let e=o.$implicit;cy(),Wf(e)}}function N(r,o){if(r&1&&(qo(0,`ul`,28),bf(1,D,2,1,`li`,29),Qa()),r&2){let e=fv().$implicit;cy(),Nf(`ngForOf`,e.list)}}function L(r,o){if(r&1){let e=lv();qo(0,`div`,30)(1,`input`,31),Ff(`change`,function(){let m=Tu(e).$implicit;return _u(fv(2).toggleCheck(m.id))}),Qa(),qo(2,`label`,32),jv(3),Qa()()}if(r&2){let e=o.$implicit,c=fv(2);cy(),Nf(`id`,e.id)(`checked`,c.isChecked(e.id)),cy(),Nf(`for`,e.id),cy(),Xa(` `,e.label,` `)}}function A(r,o){if(r&1&&(qo(0,`div`,13)(1,`div`,14)(2,`span`,15),jv(3),Qa(),qo(4,`h2`,16),jv(5),Qa()(),qo(6,`div`,17)(7,`p`,18),jv(8),Qa(),bf(9,O,1,3,`div`,19)(10,I,8,3,`div`,20)(11,N,2,1,`ul`,21)(12,L,4,4,`div`,22),Qa()()),r&2){let e=o.$implicit;cy(3),Wf(e.number),cy(2),Wf(e.title),cy(3),Wf(e.description),cy(),Nf(`ngIf`,e.alert),cy(),Nf(`ngForOf`,e.codeBlocks),cy(),Nf(`ngIf`,e.list),cy(),Nf(`ngForOf`,e.checkboxes)}}var M=class r{checkedSteps={};copiedId=null;steps=[{number:1,title:`Initial Setup - Create Directories`,description:`Create SQL Server data and backup directories:`,codeBlocks:[{id:`step1`,code:`# Run in PowerShell as Administrator
New-Item -ItemType Directory -Force -Path "C:\\mssql\\data"
New-Item -ItemType Directory -Force -Path "C:\\mssql\\backup"`}],checkboxes:[{id:`step1-check`,label:`Directories created`}]},{number:2,title:`Install SQL Server Express`,description:`Download and install SQL Server Express from Microsoft's website.`,alert:{type:`info`,message:`Download: <a href="https://www.microsoft.com/en-us/sql-server/sql-server-downloads" target="_blank">SQL Server Express Download</a>`},checkboxes:[{id:`step2a`,label:`SQL Server Express installed`},{id:`step2b`,label:`SQL Server Management Studio (SSMS) installed`},{id:`step2c`,label:`✅ RDP tested - still working`}]},{number:3,title:`Configure SQL Server`,description:`Open SSMS, connect to .\\SQLEXPRESS, then run these commands:`,codeBlocks:[{id:`step3a`,code:`-- Enable sa account and set password
USE master;
GO

ALTER LOGIN sa ENABLE;
GO

ALTER LOGIN sa WITH PASSWORD = 'sa';
GO

-- Change default data directory
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\\Microsoft\\MSSQLServer\\MSSQLServer', 
    N'DefaultData', 
    REG_SZ, 
    N'C:\\mssql\\data';
GO

-- Change default log directory  
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\\Microsoft\\MSSQLServer\\MSSQLServer', 
    N'DefaultLog', 
    REG_SZ, 
    N'C:\\mssql\\data';
GO

-- Change default backup directory
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\\Microsoft\\MSSQLServer\\MSSQLServer', 
    N'BackupDirectory', 
    REG_SZ, 
    N'C:\\mssql\\backup';
GO

-- Enable SQL Server authentication (mixed mode)
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\\Microsoft\\MSSQLServer\\MSSQLServer', 
    N'LoginMode', 
    REG_DWORD, 
    2;
GO`},{id:`step3b`,code:`# PowerShell as Administrator
Restart-Service MSSQLSERVER -Force`}],checkboxes:[{id:`step3-check`,label:`SQL Server configured`}]},{number:4,title:`Install Visual Studio 2022`,description:`Download and install VS2022. If you have a .vsconfig file, use it:`,codeBlocks:[{id:`step4`,code:`vs_professional.exe --config "C:\\Users\\Admin\\Documents\\.vsconfig"`}],checkboxes:[{id:`step4a`,label:`VS2022 installed`},{id:`step4b`,label:`✅ RDP tested - still working`}]},{number:5,title:`Install .NET SDK 9.0`,description:`Download and install .NET SDK 9.0 from Microsoft.`,alert:{type:`info`,message:`Download: <a href="https://dotnet.microsoft.com/download" target="_blank">.NET SDK Download</a>`},checkboxes:[{id:`step5a`,label:`.NET SDK 9.0 installed`},{id:`step5b`,label:`✅ RDP tested - still working`}]},{number:6,title:`Install IIS with WebSocket`,description:`Run these commands in PowerShell as Administrator:`,codeBlocks:[{id:`step6`,code:`dism /online /enable-feature /featurename:IIS-WebServerRole /all
dism /online /enable-feature /featurename:IIS-WebSockets /all
dism /online /enable-feature /featurename:IIS-ASPNET45 /all`}],checkboxes:[{id:`step6a`,label:`IIS with WebSocket installed`},{id:`step6b`,label:`✅ RDP tested - still working`}]},{number:7,title:`Create Self-Signed SSL Certificate`,description:`Run this PowerShell script as Administrator:`,codeBlocks:[{id:`step7`,code:`$serverName = "localhost"
$certExpiryYears = 10
$hostname = $env:COMPUTERNAME
$localIP = "192.168.1.107"

# Subject Alternative Names (SAN)
$san = "DNS=localhost&DNS=$hostname&IPAddress=127.0.0.1&IPAddress=::1&IPAddress=$localIP"

# Create certificate
$cert = New-SelfSignedCertificate \`
    -Subject "CN=$serverName" \`
    -KeyUsage DigitalSignature, KeyEncipherment \`
    -KeyAlgorithm RSA \`
    -KeyLength 2048 \`
    -TextExtension @(
        "2.5.29.37={text}1.3.6.1.5.5.7.3.1",
        "2.5.29.17={text}$san"
    ) \`
    -CertStoreLocation "cert:\\LocalMachine\\My" \`
    -NotAfter (Get-Date).AddYears($certExpiryYears) \`
    -FriendlyName "Local Dev Certificate"

# Export to Trusted Root
$rootStore = Get-Item "cert:\\LocalMachine\\Root"
$rootStore.Open("ReadWrite")
$rootStore.Add($cert)
$rootStore.Close()

# Output
Write-Host "\u2705 Certificate created for CN=$serverName" -ForegroundColor Green
Write-Host "\u{1F4CD} Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow
Write-Host "\u{1F512} Added to Trusted Root" -ForegroundColor Green`}],checkboxes:[{id:`step7a`,label:`SSL Certificate created`},{id:`step7b`,label:`✅ RDP tested - still working`}]},{number:8,title:`Install Git`,description:`Install Git using winget:`,codeBlocks:[{id:`step8a`,code:`winget install --id Git.Git -e --source winget`},{id:`step8b`,code:`git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"`}],checkboxes:[{id:`step8a-check`,label:`Git installed and configured`},{id:`step8b-check`,label:`✅ RDP tested - still working`}]},{number:9,title:`Install Node.js`,description:`Download and install Node.js from the official website.`,alert:{type:`info`,message:`Download: <a href="https://nodejs.org/" target="_blank">Node.js Download</a>`},checkboxes:[{id:`step9`,label:`Node.js installed`}]},{number:10,title:`Install Angular CLI`,description:`After Node.js is installed, restart terminal and run:`,codeBlocks:[{id:`step10a`,code:`npm install -g @angular/cli`},{id:`step10b`,code:`ng version`}],checkboxes:[{id:`step10a-check`,label:`Angular CLI v21 installed`},{id:`step10b-check`,label:`✅ RDP tested - still working`}]},{number:11,title:`Set PowerShell Execution Policy`,description:`Allow running npm scripts (ng commands):`,codeBlocks:[{id:`step11`,code:`Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`}],checkboxes:[{id:`step11-check`,label:`Execution policy set`}]},{number:12,title:`Configure .NET Dev Certificates`,description:`Trust the development SSL certificates:`,codeBlocks:[{id:`step12`,code:`dotnet dev-certs https --clean
dotnet dev-certs https --trust`}],alert:{type:`info`,message:`Note: Certificates are stored in %APPDATA%\\ASP.NET\\https\\`},checkboxes:[{id:`step12-check`,label:`Dev certificates configured`}]},{number:13,title:`Install WSL (Windows Subsystem for Linux)`,description:`With Windows 11 25H2, WSL should install cleanly:`,codeBlocks:[{id:`step13`,code:`# PowerShell as Administrator
wsl --install`}],checkboxes:[{id:`step13a`,label:`WSL installed and Ubuntu configured`},{id:`step13b`,label:`✅ RDP tested - still working`}]},{number:14,title:`Copy Source Code and Projects`,description:`Copy your repos to the new system. For each Angular project, run:`,codeBlocks:[{id:`step14`,code:`cd C:\\source\\repos\\YourProject
npm install`}],checkboxes:[{id:`step14-check`,label:`Source code copied and npm packages installed`}]},{number:15,title:`Restore SQL Database`,description:`In SSMS, restore your database from backup:`,list:[`Right-click Databases > Restore Database...`,`Select Device and browse to your backup file in C:\\mssql\\backup\\`,`Click OK`],checkboxes:[{id:`step15`,label:`Database restored`}]}];ngOnInit(){this.loadProgress()}loadProgress(){let o=localStorage.getItem(`win11-setup-progress`);o&&(this.checkedSteps=JSON.parse(o))}saveProgress(){localStorage.setItem(`win11-setup-progress`,JSON.stringify(this.checkedSteps))}toggleCheck(o){this.checkedSteps[o]=!this.checkedSteps[o],this.saveProgress()}copyCode(o,e){navigator.clipboard.writeText(o).then(()=>{this.copiedId=e,setTimeout(()=>{this.copiedId=null},2e3)})}isChecked(o){return this.checkedSteps[o]||!1}static ɵfac=function(e){return new(e||r)};static ɵcmp=FD({type:r,selectors:[[`app-windows11`]],decls:49,vars:1,consts:[[1,`windows11-container`],[1,`container-fluid`,`p-0`],[1,`header`,`text-center`,`py-5`],[1,`display-4`,`fw-bold`,`mb-3`],[1,`lead`],[1,`container`,`py-5`],[`role`,`alert`,1,`alert`,`alert-warning`,`d-flex`,`align-items-center`,`mb-4`],[1,`bi`,`bi-exclamation-triangle-fill`,`me-2`],[`class`,`step-container mb-5`,4,`ngFor`,`ngForOf`],[`role`,`alert`,1,`alert`,`alert-success`,`mb-4`],[1,`alert-heading`],[1,`mb-0`],[`role`,`alert`,1,`alert`,`alert-info`],[1,`step-container`,`mb-5`],[1,`step-header`,`d-flex`,`align-items-center`,`mb-3`],[1,`step-number`],[1,`h3`,`mb-0`,`ms-3`],[1,`step-content`,`ps-5`],[1,`text-muted`],[`role`,`alert`,3,`class`,`innerHTML`,4,`ngIf`],[`class`,`code-block-wrapper mb-3`,4,`ngFor`,`ngForOf`],[`class`,`mb-3`,4,`ngIf`],[`class`,`form-check`,4,`ngFor`,`ngForOf`],[`role`,`alert`,3,`innerHTML`],[1,`code-block-wrapper`,`mb-3`],[1,`code-block`,`position-relative`],[1,`btn`,`btn-sm`,`btn-primary`,`copy-btn`,3,`click`],[1,`bi`,3,`ngClass`],[1,`mb-3`],[4,`ngFor`,`ngForOf`],[1,`form-check`],[`type`,`checkbox`,1,`form-check-input`,3,`change`,`id`,`checked`],[1,`form-check-label`,3,`for`]],template:function(e,c){e&1&&(qo(0,`div`,0)(1,`div`,1)(2,`div`,2)(3,`h1`,3),jv(4,`🚀 Windows 11 Development Setup`),Qa(),qo(5,`p`,4),jv(6,`Step-by-Step Installation Guide with RDP Testing`),Qa()(),qo(7,`div`,5)(8,`div`,6),xf(9,`i`,7),qo(10,`div`)(11,`strong`),jv(12,`⚠️ Important:`),Qa(),jv(13,` Test Remote Desktop Connection to 192.168.1.158 after EACH major installation to ensure nothing breaks RDP! `),Qa()(),bf(14,A,13,7,`div`,8),qo(15,`div`,9)(16,`h4`,10),jv(17,`✅ Setup Complete!`),Qa(),qo(18,`p`),jv(19,`Your Windows 11 development environment is ready. Test your applications and verify everything works correctly.`),Qa(),xf(20,`hr`),qo(21,`p`,11)(22,`strong`),jv(23,`Final RDP Test:`),Qa(),jv(24,` Connect to 192.168.1.158 to ensure everything still works!`),Qa()(),qo(25,`div`,12)(26,`h4`,10),jv(27,`📝 Quick Reference`),Qa(),qo(28,`ul`,11)(29,`li`)(30,`strong`),jv(31,`SQL Server:`),Qa(),jv(32,` .\\SQLEXPRESS (sa/sa)`),Qa(),qo(33,`li`)(34,`strong`),jv(35,`Data Directory:`),Qa(),jv(36,` C:\\mssql\\data`),Qa(),qo(37,`li`)(38,`strong`),jv(39,`Backup Directory:`),Qa(),jv(40,` C:\\mssql\\backup`),Qa(),qo(41,`li`)(42,`strong`),jv(43,`Certificates:`),Qa(),jv(44,` %APPDATA%\\ASP.NET\\https\\`),Qa(),qo(45,`li`)(46,`strong`),jv(47,`Git Config:`),Qa(),jv(48,` C:\\Users\\Admin\\.gitconfig`),Qa()()()()()()),e&2&&(cy(14),Nf(`ngForOf`,c.steps))},dependencies:[jE,OE,Ep,RE,cn],styles:[`.windows11-container[_ngcontent-%COMP%]{min-height:100vh}.windows11-container[_ngcontent-%COMP%]   .container[_ngcontent-%COMP%]{background:#fff;border-radius:10px;box-shadow:0 10px 40px #0003;margin-top:-30px;position:relative}.windows11-container[_ngcontent-%COMP%]   .step-container[_ngcontent-%COMP%]{border-left:4px solid #667eea;padding-left:0}.windows11-container[_ngcontent-%COMP%]   .step-number[_ngcontent-%COMP%]{display:inline-flex;align-items:center;justify-content:center;width:50px;height:50px;background:#667eea;color:#fff;border-radius:50%;font-weight:700;font-size:1.25rem}.windows11-container[_ngcontent-%COMP%]   .step-content[_ngcontent-%COMP%]{border-left:2px solid #e9ecef;margin-left:25px}.windows11-container[_ngcontent-%COMP%]   .code-block-wrapper[_ngcontent-%COMP%]{position:relative}.windows11-container[_ngcontent-%COMP%]   .code-block[_ngcontent-%COMP%]{background:#2d2d2d;color:#f8f8f2;padding:1rem;border-radius:.5rem;overflow-x:auto}.windows11-container[_ngcontent-%COMP%]   .code-block[_ngcontent-%COMP%]   .copy-btn[_ngcontent-%COMP%]{position:absolute;top:10px;right:10px;z-index:10}.windows11-container[_ngcontent-%COMP%]   .code-block[_ngcontent-%COMP%]   pre[_ngcontent-%COMP%]{color:#f8f8f2;margin:0;white-space:pre-wrap;word-wrap:break-word}.windows11-container[_ngcontent-%COMP%]   .code-block[_ngcontent-%COMP%]   pre[_ngcontent-%COMP%]   code[_ngcontent-%COMP%]{color:#f8f8f2;background:transparent}.windows11-container[_ngcontent-%COMP%]   .form-check[_ngcontent-%COMP%]{margin-bottom:.5rem}.windows11-container[_ngcontent-%COMP%]   .form-check[_ngcontent-%COMP%]   .form-check-input[_ngcontent-%COMP%]{cursor:pointer;width:1.25rem;height:1.25rem}.windows11-container[_ngcontent-%COMP%]   .form-check[_ngcontent-%COMP%]   .form-check-input[_ngcontent-%COMP%]:checked{background-color:#667eea;border-color:#667eea}.windows11-container[_ngcontent-%COMP%]   .form-check[_ngcontent-%COMP%]   .form-check-label[_ngcontent-%COMP%]{cursor:pointer;margin-left:.5rem}.windows11-container[_ngcontent-%COMP%]   .alert[_ngcontent-%COMP%]   i[_ngcontent-%COMP%]{font-size:1.25rem}.code-block[_ngcontent-%COMP%]   code[_ngcontent-%COMP%]{font-family:Consolas,Monaco,Courier New,monospace;font-size:.875rem;line-height:1.5}`]})};export{M as Windows11};