---
variables:
  PLATFORM:
    description: "Target platform (windows, macos, linux)"
    required: true
  PACKAGE_MANAGER:
    description: "Package manager to use (brew, apt, yum, choco, winget)"
    required: false
    default: ""
  INSTALL_DEV_TOOLS:
    description: "Install development tools"
    required: false
    default: false
  INSTALL_DOCKER:
    description: "Install Docker"
    required: false
    default: false
  USER_SHELL:
    description: "User's shell (bash, zsh, fish, powershell, cmd)"
    required: false
    default: "bash"
---

# Development Environment Setup

**Target Platform:** {{PLATFORM}}

---

## Overview

This guide will help you set up your development environment for building and running our application.

{{#if PLATFORM == 'windows'}}
**Platform:** Microsoft Windows

This guide assumes you are using Windows 10 or later.
{{else if PLATFORM == 'macos'}}
**Platform:** macOS

This guide assumes you are using macOS 10.15 (Catalina) or later.
{{else if PLATFORM == 'linux'}}
**Platform:** Linux

This guide covers common Linux distributions (Ubuntu, Debian, Fedora, CentOS).
{{else}}
**ERROR:** Unknown platform specified: {{PLATFORM}}
Valid options: windows, macos, linux
{{/if}}

---

## Prerequisites

{{#if PLATFORM == 'windows'}}
### Windows Prerequisites

- **Operating System:** Windows 10 (64-bit) or later
- **PowerShell:** Version 5.1 or later (run `$PSVersionTable.PSVersion` to check)
- **Administrator Access:** Required for installing some software

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'choco'}}
**Package Manager:** Using Chocolatey
- If not installed, visit: https://chocolatey.org/install
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'winget'}}
**Package Manager:** Using Windows Package Manager (winget)
- Included with Windows 11 and Windows 10 (version 1809+)
{{else}}
**Package Manager:** Manual installation
- You will download and install software manually
{{/if}}

{{else if PLATFORM == 'macos'}}
### macOS Prerequisites

- **Operating System:** macOS 10.15 (Catalina) or later
- **Xcode Command Line Tools:** Required for development

First, install Xcode Command Line Tools:
```bash
xcode-select --install
```

{{#if !exists(PACKAGE_MANAGER) || PACKAGE_MANAGER == 'brew'}}
**Package Manager:** Using Homebrew

If Homebrew is not installed, install it:
```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

Verify installation:
```bash
brew --version
```
{{else}}
**Package Manager:** Manual installation
- You will download and install software manually from .dmg files
{{/if}}

{{else if PLATFORM == 'linux'}}
### Linux Prerequisites

- **Operating System:** Recent Linux distribution with kernel 4.0+

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'apt'}}
**Distribution:** Debian/Ubuntu-based
**Package Manager:** APT

Update package lists:
```bash
sudo apt update
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'yum'}}
**Distribution:** RHEL/CentOS/Fedora-based
**Package Manager:** YUM/DNF

Update package lists:
```bash
sudo yum update
# Or for newer Fedora:
# sudo dnf update
```
{{else}}
**Note:** Commands shown will use generic package manager syntax.
Adjust for your specific distribution.
{{/if}}

{{/if}}

---

## Step 1: Install Node.js

Node.js is required to build and run the application.

{{#if PLATFORM == 'windows'}}
### Installing Node.js on Windows

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'choco'}}
**Using Chocolatey:**
```powershell
choco install nodejs-lts -y
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'winget'}}
**Using winget:**
```powershell
winget install OpenJS.NodeJS.LTS
```
{{else}}
**Manual Installation:**
1. Download the Windows Installer (.msi) from: https://nodejs.org/
2. Choose the LTS (Long Term Support) version
3. Run the installer and follow the prompts
4. Restart your terminal after installation
{{/if}}

**Verify installation:**
```powershell
node --version
npm --version
```

{{else if PLATFORM == 'macos'}}
### Installing Node.js on macOS

{{#if !exists(PACKAGE_MANAGER) || PACKAGE_MANAGER == 'brew'}}
**Using Homebrew:**
```bash
brew install node@20
```

**Add to PATH (if needed):**
{{#if USER_SHELL == 'zsh'}}
```bash
echo 'export PATH="/usr/local/opt/node@20/bin:$PATH"' >> ~/.zshrc
source ~/.zshrc
```
{{else if USER_SHELL == 'bash'}}
```bash
echo 'export PATH="/usr/local/opt/node@20/bin:$PATH"' >> ~/.bash_profile
source ~/.bash_profile
```
{{else if USER_SHELL == 'fish'}}
```bash
set -Ua fish_user_paths /usr/local/opt/node@20/bin
```
{{else}}
```bash
echo 'export PATH="/usr/local/opt/node@20/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc
```
{{/if}}
{{else}}
**Manual Installation:**
1. Download the macOS Installer (.pkg) from: https://nodejs.org/
2. Choose the LTS (Long Term Support) version
3. Run the installer and follow the prompts
{{/if}}

**Verify installation:**
```bash
node --version
npm --version
```

{{else if PLATFORM == 'linux'}}
### Installing Node.js on Linux

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'apt'}}
**Using APT (Ubuntu/Debian):**
```bash
# Install Node.js 20.x
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'yum'}}
**Using YUM/DNF (RHEL/CentOS/Fedora):**
```bash
# Install Node.js 20.x
curl -fsSL https://rpm.nodesource.com/setup_20.x | sudo bash -
sudo yum install -y nodejs
# Or for DNF:
# sudo dnf install -y nodejs
```
{{else}}
**Using Node Version Manager (nvm) - Universal:**
```bash
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.0/install.sh | bash
source ~/.bashrc  # or ~/.zshrc
nvm install 20
nvm use 20
```
{{/if}}

**Verify installation:**
```bash
node --version
npm --version
```

{{/if}}

---

## Step 2: Install Git

{{#if PLATFORM == 'windows'}}
### Installing Git on Windows

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'choco'}}
**Using Chocolatey:**
```powershell
choco install git -y
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'winget'}}
**Using winget:**
```powershell
winget install Git.Git
```
{{else}}
**Manual Installation:**
1. Download Git for Windows from: https://git-scm.com/download/win
2. Run the installer
3. Use default settings (Git Bash is recommended)
{{/if}}

**Verify installation:**
```powershell
git --version
```

{{else if PLATFORM == 'macos'}}
### Installing Git on macOS

Git is included with Xcode Command Line Tools (already installed in prerequisites).

{{#if !exists(PACKAGE_MANAGER) || PACKAGE_MANAGER == 'brew'}}
**To get the latest version via Homebrew:**
```bash
brew install git
```
{{/if}}

**Verify installation:**
```bash
git --version
```

{{else if PLATFORM == 'linux'}}
### Installing Git on Linux

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'apt'}}
**Using APT:**
```bash
sudo apt install -y git
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'yum'}}
**Using YUM/DNF:**
```bash
sudo yum install -y git
# Or: sudo dnf install -y git
```
{{else}}
Git is usually pre-installed on most Linux distributions.
{{/if}}

**Verify installation:**
```bash
git --version
```

{{/if}}

---

{{#if INSTALL_DEV_TOOLS}}
## Step 3: Install Development Tools

{{#if PLATFORM == 'windows'}}
### Development Tools for Windows

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'choco'}}
**Using Chocolatey:**
```powershell
# Visual Studio Code
choco install vscode -y

# Windows Terminal (recommended)
choco install microsoft-windows-terminal -y

# .NET SDK (if needed)
choco install dotnet-sdk -y

# Build tools
choco install visualstudio2022buildtools -y
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'winget'}}
**Using winget:**
```powershell
# Visual Studio Code
winget install Microsoft.VisualStudioCode

# Windows Terminal
winget install Microsoft.WindowsTerminal

# .NET SDK
winget install Microsoft.DotNet.SDK.8
```
{{else}}
**Manual Installation:**
- Visual Studio Code: https://code.visualstudio.com/
- Windows Terminal: Microsoft Store
- .NET SDK: https://dotnet.microsoft.com/download
{{/if}}

{{else if PLATFORM == 'macos'}}
### Development Tools for macOS

{{#if !exists(PACKAGE_MANAGER) || PACKAGE_MANAGER == 'brew'}}
**Using Homebrew:**
```bash
# Visual Studio Code
brew install --cask visual-studio-code

# Development utilities
brew install wget curl jq

# Optional: .NET SDK
brew install --cask dotnet-sdk
```
{{else}}
**Manual Installation:**
- Visual Studio Code: https://code.visualstudio.com/
- .NET SDK: https://dotnet.microsoft.com/download
{{/if}}

{{else if PLATFORM == 'linux'}}
### Development Tools for Linux

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'apt'}}
**Using APT:**
```bash
# Essential build tools
sudo apt install -y build-essential

# Development utilities
sudo apt install -y wget curl jq vim

# Visual Studio Code
wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > packages.microsoft.gpg
sudo install -D -o root -g root -m 644 packages.microsoft.gpg /etc/apt/keyrings/packages.microsoft.gpg
sudo sh -c 'echo "deb [arch=amd64,arm64,armhf signed-by=/etc/apt/keyrings/packages.microsoft.gpg] https://packages.microsoft.com/repos/code stable main" > /etc/apt/sources.list.d/vscode.list'
sudo apt update
sudo apt install -y code
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'yum'}}
**Using YUM/DNF:**
```bash
# Development tools
sudo yum groupinstall -y "Development Tools"

# Utilities
sudo yum install -y wget curl jq vim

# Visual Studio Code
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
sudo sh -c 'echo -e "[code]\nname=Visual Studio Code\nbaseurl=https://packages.microsoft.com/yumrepos/vscode\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" > /etc/yum.repos.d/vscode.repo'
sudo yum install -y code
```
{{/if}}

{{/if}}

{{else}}
## Step 3: Optional Development Tools

You chose not to install additional development tools. You can install them later if needed.

{{/if}}

---

{{#if INSTALL_DOCKER}}
## Step 4: Install Docker

Docker is useful for containerized development and testing.

{{#if PLATFORM == 'windows'}}
### Installing Docker on Windows

**Docker Desktop for Windows:**

1. Download Docker Desktop from: https://www.docker.com/products/docker-desktop
2. System requirements:
   - Windows 10 64-bit: Pro, Enterprise, or Education (Build 19041 or higher)
   - WSL 2 backend enabled (recommended)
   - Virtualization enabled in BIOS

3. Install and restart your computer

4. Start Docker Desktop from the Start menu

**Verify installation:**
```powershell
docker --version
docker compose version
```

**Enable WSL 2 backend (recommended):**
```powershell
wsl --install
wsl --set-default-version 2
```

{{else if PLATFORM == 'macos'}}
### Installing Docker on macOS

{{#if !exists(PACKAGE_MANAGER) || PACKAGE_MANAGER == 'brew'}}
**Using Homebrew:**
```bash
brew install --cask docker
```

Open Docker from Applications folder after installation.
{{else}}
**Docker Desktop for Mac:**

1. Download Docker Desktop from: https://www.docker.com/products/docker-desktop
2. Choose the version for your chip:
   - Apple Silicon (M1/M2): ARM64 version
   - Intel: AMD64 version
3. Drag Docker.app to Applications
4. Open Docker from Applications
{{/if}}

**Verify installation:**
```bash
docker --version
docker compose version
```

{{else if PLATFORM == 'linux'}}
### Installing Docker on Linux

{{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'apt'}}
**Using APT (Ubuntu/Debian):**
```bash
# Remove old versions
sudo apt remove -y docker docker-engine docker.io containerd runc

# Install dependencies
sudo apt install -y ca-certificates curl gnupg lsb-release

# Add Docker's official GPG key
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg

# Set up repository
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker Engine
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Add your user to docker group
sudo usermod -aG docker $USER
```
{{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'yum'}}
**Using YUM/DNF (RHEL/CentOS/Fedora):**
```bash
# Remove old versions
sudo yum remove -y docker docker-client docker-client-latest docker-common docker-latest

# Install dependencies
sudo yum install -y yum-utils

# Set up repository
sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo

# Install Docker Engine
sudo yum install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Start Docker
sudo systemctl start docker
sudo systemctl enable docker

# Add your user to docker group
sudo usermod -aG docker $USER
```
{{/if}}

**Log out and log back in** for group changes to take effect.

**Verify installation:**
```bash
docker --version
docker compose version
```

{{/if}}

{{else}}
## Step 4: Docker Installation Skipped

You chose not to install Docker. You can install it later if needed for containerized development.

{{/if}}

---

## Step 5: Clone and Setup Project

{{#if PLATFORM == 'windows'}}
**On Windows (PowerShell):**
```powershell
# Create workspace directory
New-Item -ItemType Directory -Force -Path C:\workspace
Set-Location C:\workspace

# Clone repository
git clone https://github.com/your-org/your-project.git
Set-Location your-project

# Install dependencies
npm install

# Copy environment template
Copy-Item .env.example .env

# Start development server
npm run dev
```

{{else if PLATFORM == 'macos'}}
**On macOS:**
```bash
# Create workspace directory
mkdir -p ~/workspace
cd ~/workspace

# Clone repository
git clone https://github.com/your-org/your-project.git
cd your-project

# Install dependencies
npm install

# Copy environment template
cp .env.example .env

# Start development server
npm run dev
```

{{else if PLATFORM == 'linux'}}
**On Linux:**
```bash
# Create workspace directory
mkdir -p ~/workspace
cd ~/workspace

# Clone repository
git clone https://github.com/your-org/your-project.git
cd your-project

# Install dependencies
npm install

# Copy environment template
cp .env.example .env

# Start development server
npm run dev
```

{{/if}}

---

## Troubleshooting

{{#if PLATFORM == 'windows'}}
### Common Windows Issues

**Issue: Command not found after installation**
- **Solution:** Restart PowerShell or add the program to your PATH manually
- Check System Properties → Environment Variables → Path

**Issue: npm install fails with permission errors**
- **Solution:** Run PowerShell as Administrator for the installation

**Issue: Scripts disabled error**
- **Solution:** Enable script execution:
  ```powershell
  Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
  ```

{{else if PLATFORM == 'macos'}}
### Common macOS Issues

**Issue: Permission denied when running commands**
- **Solution:** Don't use sudo with npm. Fix npm permissions:
  ```bash
  mkdir ~/.npm-global
  npm config set prefix '~/.npm-global'
  echo 'export PATH=~/.npm-global/bin:$PATH' >> ~/.zshrc
  source ~/.zshrc
  ```

**Issue: Xcode Command Line Tools not installed**
- **Solution:** Run `xcode-select --install`

**Issue: Homebrew command not found**
- **Solution:** Check if Homebrew is in your PATH:
  ```bash
  echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
  eval "$(/opt/homebrew/bin/brew shellenv)"
  ```

{{else if PLATFORM == 'linux'}}
### Common Linux Issues

**Issue: Permission denied accessing Docker**
- **Solution:** Make sure you're in the docker group and have logged out/in:
  ```bash
  sudo usermod -aG docker $USER
  # Then log out and log back in
  ```

**Issue: npm install fails with EACCES**
- **Solution:** Don't use sudo with npm. Fix npm permissions:
  ```bash
  mkdir ~/.npm-global
  npm config set prefix '~/.npm-global'
  echo 'export PATH=~/.npm-global/bin:$PATH' >> ~/.bashrc
  source ~/.bashrc
  ```

**Issue: Build tools missing**
- **Solution:** Install build essentials for your distribution
  {{#if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'apt'}}
  ```bash
  sudo apt install build-essential
  ```
  {{else if exists(PACKAGE_MANAGER) && PACKAGE_MANAGER == 'yum'}}
  ```bash
  sudo yum groupinstall "Development Tools"
  ```
  {{/if}}

{{/if}}

---

## Next Steps

{{#if PLATFORM == 'windows'}}
Your Windows development environment is ready!
{{else if PLATFORM == 'macos'}}
Your macOS development environment is ready!
{{else if PLATFORM == 'linux'}}
Your Linux development environment is ready!
{{/if}}

**Recommended next steps:**

1. Configure your IDE with recommended extensions
2. Read the project documentation in `docs/`
3. Review the coding standards and contribution guidelines
4. Set up your Git identity:
   {{#if PLATFORM == 'windows'}}
   ```powershell
   git config --global user.name "Your Name"
   git config --global user.email "your.email@example.com"
   ```
   {{else}}
   ```bash
   git config --global user.name "Your Name"
   git config --global user.email "your.email@example.com"
   ```
   {{/if}}

5. Join the development team chat/Slack channel

{{#if INSTALL_DOCKER}}
**Docker Tips:**
- Use `docker compose up` to start all services
- Use `docker compose down` to stop all services
- Check `docker-compose.yml` for available services
{{/if}}

Happy coding!
