# Website and App Blocker

![License](https://img.shields.io/github/license/MohammedTsmu/WebsiteAndAppBlocker)
![Issues](https://img.shields.io/github/issues/MohammedTsmu/WebsiteAndAppBlocker)
![Stars](https://img.shields.io/github/stars/MohammedTsmu/WebsiteAndAppBlocker?style=social)

## Overview

**Website and App Blocker** is an open-source application designed to help users manage and restrict access to specific websites and applications during designated study hours. This tool is ideal for students and professionals seeking to enhance productivity by minimizing distractions.

## Features

- **Block Websites:** Prevent access to distracting websites by modifying the system's hosts file.
- **Block Applications:** Automatically terminate specified applications during study periods.
- **Scheduling:** Define blocking periods (e.g., 8 AM to 6 PM) to enforce focus times.
- **Authentication:** Secure access to settings and unblocking features with password protection.
- **System Tray Integration:** Minimize the application to the system tray for unobtrusive operation.
- **Challenge Verification:** Ensure intentional unblocking actions through challenge prompts.
- **User-Friendly Interface:** Easy-to-navigate UI for managing blocked items and settings.

## Screenshots

![Main Window](screenshots/main_window.png)
*Main interface for managing blocked websites and applications.*

![Login Window](screenshots/login_window.png)
*Secure login prompt.*

![About Window](screenshots/about_window.png)
*Information about the application.*

## Installation

1. **Download the Latest Release:**
   - Visit the [Releases](https://github.com/MohammedTsmu/WebsiteAndAppBlocker/releases) page.
   - Download the installer or the portable version as per your preference.

2. **Run the Installer:**
   - Execute the downloaded installer and follow the on-screen instructions.
   - Ensure you have administrative privileges as the application modifies system files.

3. **Launch the Application:**
   - After installation, launch **Website and App Blocker** from the Start Menu or Desktop shortcut.

## Usage

1. **Initial Setup:**
   - On the first run, you'll be prompted to log in. Since no password is set initially, simply click **Login** to access the main window.

2. **Setting a Password:**
   - Navigate to the **Settings** tab.
   - Enter a new password and confirm it.
   - Click **Set Password** to secure the application.

3. **Blocking Websites:**
   - In the **Websites** section, enter the URL of the website you wish to block.
   - Click **Block Website** to add it to the blocked list.

4. **Blocking Applications:**
   - In the **Applications** section, enter the name of the application executable (e.g., `chrome.exe`).
   - Click **Block Application** to add it to the blocked list.

5. **Unblocking Items:**
   - To unblock a website or application, select it from the blocked list and click the **Unblock** button.
   - You'll be prompted to authenticate and complete a challenge before the item is unblocked.

6. **System Tray Operations:**
   - Minimize the application to the system tray.
   - Right-click the tray icon to access options like **Open** or **Exit**.

## Contributing

Contributions are welcome! If you'd like to improve **Website and App Blocker**, please follow these steps:

1. **Fork the Repository:**
   - Click the **Fork** button at the top-right corner of the repository page.

2. **Clone the Forked Repository:**
   ```bash
   git clone https://github.com/YourUsername/WebsiteAndAppBlocker.git

License
© 2023 Dr. Mohammed Studio. All rights reserved.
Distribution or modification under the AGPL-3.0 License is allowed.



---

### **Notes:**

1. **License Hyperlink:**
   - Ensure that the `LICENSE` file exists in the root of your GitHub repository.
   - The hyperlink in the **AboutWindow** points to `https://github.com/MohammedTsmu/WebsiteAndAppBlocker/blob/main/LICENSE`. Adjust the URL if your license file is located elsewhere or if the repository structure changes.

2. **README.md Structure:**
   - The provided `README.md` includes sections like **Overview**, **Features**, **Installation**, **Usage**, **Contributing**, **License**, **Support**, and **Acknowledgements**.
   - Customize the **Screenshots** section by adding actual screenshots of your application in the `screenshots` directory and updating the image paths accordingly.

3. **Exception Handling in AboutWindow:**
   - The `Hyperlink_RequestNavigate` method in `AboutWindow.xaml.cs` ensures that clicking the license link opens it in the default web browser.
   - Proper error handling is included to notify the user if the link fails to open.

4. **Enhancing the UI:**
   - Feel free to further customize the appearance of the **AboutWindow** by adjusting colors, fonts, and layout as per your preferences.

5. **Testing:**
   - After implementing these changes, thoroughly test the **AboutWindow** to ensure that the license link works correctly and that the `README.md` displays well on GitHub.

---

**If you need any further assistance or additional features, feel free to ask! I'm here to help you enhance your application.**
