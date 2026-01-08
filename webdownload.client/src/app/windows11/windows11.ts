import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface Step {
  number: number;
  title: string;
  description: string;
  codeBlocks?: CodeBlock[];
  checkboxes: CheckboxItem[];
  alert?: { type: string; message: string };
  list?: string[];
}

interface CodeBlock {
  id: string;
  code: string;
  language?: string;
}

interface CheckboxItem {
  id: string;
  label: string;
}

@Component({
  selector: 'app-windows11',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './windows11.html',
  styleUrls: ['./windows11.scss']
})
export class Windows11 implements OnInit {
  checkedSteps: { [key: string]: boolean } = {};
  copiedId: string | null = null;

  steps: Step[] = [
    {
      number: 1,
      title: 'Initial Setup - Create Directories',
      description: 'Create SQL Server data and backup directories:',
      codeBlocks: [{
        id: 'step1',
        code: `# Run in PowerShell as Administrator
New-Item -ItemType Directory -Force -Path "C:\\mssql\\data"
New-Item -ItemType Directory -Force -Path "C:\\mssql\\backup"`
      }],
      checkboxes: [{ id: 'step1-check', label: 'Directories created' }]
    },
    {
      number: 2,
      title: 'Install SQL Server Express',
      description: 'Download and install SQL Server Express from Microsoft\'s website.',
      alert: {
        type: 'info',
        message: 'Download: <a href="https://www.microsoft.com/en-us/sql-server/sql-server-downloads" target="_blank">SQL Server Express Download</a>'
      },
      checkboxes: [
        { id: 'step2a', label: 'SQL Server Express installed' },
        { id: 'step2b', label: 'SQL Server Management Studio (SSMS) installed' },
        { id: 'step2c', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 3,
      title: 'Configure SQL Server',
      description: 'Open SSMS, connect to .\\SQLEXPRESS, then run these commands:',
      codeBlocks: [{
        id: 'step3a',
        code: `-- Enable sa account and set password
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
GO`
      },
      {
        id: 'step3b',
        code: `# PowerShell as Administrator
Restart-Service MSSQLSERVER -Force`
      }],
      checkboxes: [{ id: 'step3-check', label: 'SQL Server configured' }]
    },
    {
      number: 4,
      title: 'Install Visual Studio 2022',
      description: 'Download and install VS2022. If you have a .vsconfig file, use it:',
      codeBlocks: [{
        id: 'step4',
        code: `vs_professional.exe --config "C:\\Users\\Admin\\Documents\\.vsconfig"`
      }],
      checkboxes: [
        { id: 'step4a', label: 'VS2022 installed' },
        { id: 'step4b', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 5,
      title: 'Install .NET SDK 9.0',
      description: 'Download and install .NET SDK 9.0 from Microsoft.',
      alert: {
        type: 'info',
        message: 'Download: <a href="https://dotnet.microsoft.com/download" target="_blank">.NET SDK Download</a>'
      },
      checkboxes: [
        { id: 'step5a', label: '.NET SDK 9.0 installed' },
        { id: 'step5b', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 6,
      title: 'Install IIS with WebSocket',
      description: 'Run these commands in PowerShell as Administrator:',
      codeBlocks: [{
        id: 'step6',
        code: `dism /online /enable-feature /featurename:IIS-WebServerRole /all
dism /online /enable-feature /featurename:IIS-WebSockets /all
dism /online /enable-feature /featurename:IIS-ASPNET45 /all`
      }],
      checkboxes: [
        { id: 'step6a', label: 'IIS with WebSocket installed' },
        { id: 'step6b', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 7,
      title: 'Create Self-Signed SSL Certificate',
      description: 'Run this PowerShell script as Administrator:',
      codeBlocks: [{
        id: 'step7',
        code: `$serverName = "localhost"
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
Write-Host "✅ Certificate created for CN=$serverName" -ForegroundColor Green
Write-Host "📍 Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow
Write-Host "🔒 Added to Trusted Root" -ForegroundColor Green`
      }],
      checkboxes: [
        { id: 'step7a', label: 'SSL Certificate created' },
        { id: 'step7b', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 8,
      title: 'Install Git',
      description: 'Install Git using winget:',
      codeBlocks: [
        {
          id: 'step8a',
          code: `winget install --id Git.Git -e --source winget`
        },
        {
          id: 'step8b',
          code: `git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"`
        }
      ],
      checkboxes: [
        { id: 'step8a-check', label: 'Git installed and configured' },
        { id: 'step8b-check', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 9,
      title: 'Install Node.js',
      description: 'Download and install Node.js from the official website.',
      alert: {
        type: 'info',
        message: 'Download: <a href="https://nodejs.org/" target="_blank">Node.js Download</a>'
      },
      checkboxes: [{ id: 'step9', label: 'Node.js installed' }]
    },
    {
      number: 10,
      title: 'Install Angular CLI',
      description: 'After Node.js is installed, restart terminal and run:',
      codeBlocks: [
        {
          id: 'step10a',
          code: `npm install -g @angular/cli`
        },
        {
          id: 'step10b',
          code: `ng version`
        }
      ],
      checkboxes: [
        { id: 'step10a-check', label: 'Angular CLI v21 installed' },
        { id: 'step10b-check', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 11,
      title: 'Set PowerShell Execution Policy',
      description: 'Allow running npm scripts (ng commands):',
      codeBlocks: [{
        id: 'step11',
        code: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`
      }],
      checkboxes: [{ id: 'step11-check', label: 'Execution policy set' }]
    },
    {
      number: 12,
      title: 'Configure .NET Dev Certificates',
      description: 'Trust the development SSL certificates:',
      codeBlocks: [{
        id: 'step12',
        code: `dotnet dev-certs https --clean
dotnet dev-certs https --trust`
      }],
      alert: {
        type: 'info',
        message: 'Note: Certificates are stored in %APPDATA%\\ASP.NET\\https\\'
      },
      checkboxes: [{ id: 'step12-check', label: 'Dev certificates configured' }]
    },
    {
      number: 13,
      title: 'Install WSL (Windows Subsystem for Linux)',
      description: 'With Windows 11 25H2, WSL should install cleanly:',
      codeBlocks: [{
        id: 'step13',
        code: `# PowerShell as Administrator
wsl --install`
      }],
      checkboxes: [
        { id: 'step13a', label: 'WSL installed and Ubuntu configured' },
        { id: 'step13b', label: '✅ RDP tested - still working' }
      ]
    },
    {
      number: 14,
      title: 'Copy Source Code and Projects',
      description: 'Copy your repos to the new system. For each Angular project, run:',
      codeBlocks: [{
        id: 'step14',
        code: `cd C:\\source\\repos\\YourProject
npm install`
      }],
      checkboxes: [{ id: 'step14-check', label: 'Source code copied and npm packages installed' }]
    },
    {
      number: 15,
      title: 'Restore SQL Database',
      description: 'In SSMS, restore your database from backup:',
      list: [
        'Right-click Databases > Restore Database...',
        'Select Device and browse to your backup file in C:\\mssql\\backup\\',
        'Click OK'
      ],
      checkboxes: [{ id: 'step15', label: 'Database restored' }]
    }
  ];

  ngOnInit(): void {
    this.loadProgress();
  }

  loadProgress(): void {
    const saved = localStorage.getItem('win11-setup-progress');
    if (saved) {
      this.checkedSteps = JSON.parse(saved);
    }
  }

  saveProgress(): void {
    localStorage.setItem('win11-setup-progress', JSON.stringify(this.checkedSteps));
  }

  toggleCheck(id: string): void {
    this.checkedSteps[id] = !this.checkedSteps[id];
    this.saveProgress();
  }

  copyCode(code: string, id: string): void {
    navigator.clipboard.writeText(code).then(() => {
      this.copiedId = id;
      setTimeout(() => {
        this.copiedId = null;
      }, 2000);
    });
  }

  isChecked(id: string): boolean {
    return this.checkedSteps[id] || false;
  }
}
