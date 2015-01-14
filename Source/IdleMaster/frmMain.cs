﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using HtmlAgilityPack;
using Steamworks;

namespace IdleMaster
{
    public partial class frmMain : Form
    {
        public Process Idle; // This process handle will control steam-idle.exe
        public Dictionary<string, string> badgesLeft = new Dictionary<string, string>();
        public Boolean cookieReady = false;
        public Boolean steamReady = false;
        public int timeLeft = 900;
        public int totalCardsRemaining;
        public int totalGamesRemaining;
        public String currentAppID;

        public string cookie_sessionid = "";
        public string cookie_steamLogin = "";
        public string cookie_profileURL = "";
        
        public CookieContainer generateCookies()
        {
            CookieContainer cookies = new CookieContainer();
            Uri target = new Uri("http://steamcommunity.com");
            cookies.Add(new Cookie("sessionid", cookie_sessionid) { Domain = target.Host });
            cookies.Add(new Cookie("steamLogin", cookie_steamLogin) { Domain = target.Host });
            return cookies;
        }

        public string GetAppName(String appid)
        {            
            WebRequest request = WebRequest.Create("http://store.steampowered.com/api/appdetails/?appids=" + appid + "&filters=basic");
            WebResponse response = request.GetResponse();
            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream, Encoding.UTF8);
            string api_raw = reader.ReadToEnd();            
            string name = Regex.Match(api_raw, "\"game\",\"name\":\"(.+?)\"").Groups[1].Value;
            name = name.Replace("\\u00ae", "®");
            name = name.Replace("\\u2122", "™");
            reader.Close();
            response.Close();
            return name;
        }

        private void CopyResource(string resourceName, string file)
        {
            using (Stream resource = GetType().Assembly.GetManifestResourceStream(resourceName))
            {
                if (resource == null)
                {
                    return;
                }
                using (Stream output = File.OpenWrite(file))
                {
                    resource.CopyTo(output);
                }
            }
        }

        public void SortBadges(String method)
        {
            Dictionary<string, string> tempBadgesLeft = new Dictionary<string, string>();
            switch (method)
            {
                case "mostcards":
                    var mcitems = from pair in badgesLeft
                            orderby pair.Value descending
                            select pair;

                    foreach (KeyValuePair<string, string> pair in mcitems)
                    {
                        tempBadgesLeft.Add(pair.Key, pair.Value);
                    }
                    break;
                case "leastcards":
                    var lcitems = from pair in badgesLeft
                                orderby pair.Value ascending
                                select pair;

                    foreach (KeyValuePair<string, string> pair in lcitems)
                    {
                        tempBadgesLeft.Add(pair.Key, pair.Value);
                    }
                    break;
                default:
                    return;
            }

            badgesLeft.Clear();
            badgesLeft = tempBadgesLeft;
        }

        public void startIdle(String appid)
        {            
            // Place user "In game" for card drops
            ProcessStartInfo startInfo = new ProcessStartInfo("steam-idle.exe", appid);
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Idle = Process.Start(startInfo);
            
            // Update game name
            lblGameName.Visible = true;
            lblGameName.Text = GetAppName(appid);

            // Update game image
            picApp.Load("http://cdn.akamai.steamstatic.com/steam/apps/" + appid + "/header_292x136.jpg");
            picApp.Visible = true;

            // Update label controls
            lblCurrentRemaining.Text = badgesLeft[appid] + " card drops remaining";
            lblCurrentStatus.Text = "Currently in-game";

            // Set progress bar values and show the footer
            pbIdle.Maximum = Int32.Parse(badgesLeft[appid]);
            pbIdle.Value = 0;
            ssFooter.Visible = true;

            // Start the animated "working" gif
            picIdleStatus.Image = Properties.Resources.imgSpin;

            // Set the currentAppID value
            currentAppID = appid;

            // Start the timer that will check if drops remain
            tmrCardDropCheck.Enabled = true;

            // Reset the timer
            if (pbIdle.Maximum != 1)
            {
                timeLeft = 900;
            }
            else
            {
                timeLeft = 300;
            } 

            // Set the correct buttons on the form for pause / resume
            btnResume.Visible = false;
            btnPause.Visible = true;
            resumeIdlingToolStripMenuItem.Enabled = false;
            pauseIdlingToolStripMenuItem.Enabled = false;
            skipGameToolStripMenuItem.Enabled = false;

            Graphics graphics = this.CreateGraphics();
            double scale = graphics.DpiY * 3.86;
            this.Height = Convert.ToInt32(scale);
        }

        public void stopIdle()
        {
            try 
            {
                lblGameName.Visible = false;
                picApp.Image = null;
                picApp.Visible = false;
                lblCurrentStatus.Text = "Not in game";
                picIdleStatus.Image = null;

                // Stop the card drop check timer
                tmrCardDropCheck.Enabled = false;

                // Hide the status bar
                ssFooter.Visible = false;

                // Resize the form
                Graphics graphics = this.CreateGraphics();
                double scale = graphics.DpiY * 1.9583;
                this.Height = Convert.ToInt32(scale);

                // Kill the idling process
                Idle.Kill();
            }
            catch (Exception)
            {

            }
        }

        public void idleComplete()
        {
            // Deactivate the timer control and inform the user that the program is finished
            tmrCardDropCheck.Enabled = false;
            lblCurrentStatus.Text = "Idling complete";
            picIdleStatus.Image = null;
        }

        public async Task<string> GetHttpAsync(String url, CookieContainer cookies)
        {
            String content = "";
            try
            {
                HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
                r.Method = "GET";
                r.CookieContainer = cookies;            
                HttpWebResponse res = (HttpWebResponse)await r.GetResponseAsync();
                if (res != null)
                {
                    if (res.StatusCode == HttpStatusCode.OK)
                    {
                        Stream stream = res.GetResponseStream();
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            content = reader.ReadToEnd();
                        }
                    }
                }
                picIdleStatus.Image = Properties.Resources.imgSpin;
            }
            catch (Exception)
            {
                // Try again in 60 seconds
                timeLeft = 60;
                picIdleStatus.Image = Properties.Resources.imgFalse;
            }
            return content;
        }

        public async Task LoadBadgesAsync()
        {
            CookieContainer cookies = generateCookies();
            string response = await GetHttpAsync(cookie_profileURL + "/badges/", cookies);
            HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();
            document.LoadHtml(response);
            HtmlNodeCollection user_avatar = document.DocumentNode.SelectNodes("//div[contains(@class,'user_avatar')]");
            try
            {
                int Count = user_avatar.Count;
            }
            catch (Exception)
            {
                // Invalid cookie data

                // Clear the settings
                cookie_sessionid = "";
                cookie_steamLogin = "";
                cookie_profileURL = "";

                // Stop the steam-idle process
                if (Idle != null)
                {
                    stopIdle();
                }

                // Clear the badges list
                badgesLeft.Clear();

                // Resize the form
                Graphics graphics = this.CreateGraphics();
                double scale = graphics.DpiY * 1.625;
                this.Height = Convert.ToInt32(scale);

                // Set timer intervals
                tmrCheckSteam.Interval = 500;
                tmrCheckCookieData.Interval = 500;

                // Hide lblDrops and lblIdle
                lblDrops.Visible = false;
                lblIdle.Visible = false;

                // Set cookieReady to false
                cookieReady = false;

                // Re-enable tmrReadyToGo
                tmrReadyToGo.Enabled = true; 
                picReadingPage.Visible = false;
                return;
            }

            int totaldrops = 0;

            foreach (HtmlNode badge in document.DocumentNode.SelectNodes("//div[contains(@class,'badge_title_stats')]"))
            {
                string appid = Regex.Match(badge.InnerHtml, @"card_drop_info_gamebadge_(\d+)_").Groups[1].Value;
                HtmlNodeCollection row = badge.SelectNodes(".//span[contains(@class, 'progress_info_bold')]");
                if (row != null)
                {                    
                    foreach (HtmlNode data in row)
                    {
                        if (data != null)
                        {
                            if (Regex.Match(data.InnerHtml, @"\d").Length > 0)
                            {
                                string remaining = Regex.Match(data.InnerHtml, @"(\d+)").Groups[1].Value;
                                totaldrops = totaldrops + Convert.ToInt16(remaining);
                                if (badgesLeft.ContainsKey(appid) == false)
                                {
                                    Boolean onBlacklist = false; ;
                                    foreach (String blApp in Properties.Settings.Default.blacklist)
                                    {
                                        if (blApp == appid)
                                        {
                                            onBlacklist = true;
                                        }
                                    }
                                    if (onBlacklist == false)
                                    {
                                        badgesLeft.Add(appid, remaining);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Detect if the user has multiple badge pages
            HtmlNode pagelink = document.DocumentNode.SelectSingleNode("//a[contains(@class,'pagelink')][last()]");
            try
            {
                // Get number of pages from the last pagelink a element
                int last = Int32.Parse(Regex.Match(pagelink.Attributes["href"].Value, @"(\d+)").Groups[1].Value);
                int i = 1;
                do
                {
                    i++;
                    response = await GetHttpAsync(cookie_profileURL + "/badges/?p=" + i, cookies);
                    document = new HtmlAgilityPack.HtmlDocument();
                    document.LoadHtml(response);

                    foreach (HtmlNode badge in document.DocumentNode.SelectNodes("//div[contains(@class,'badge_title_stats')]"))
                    {
                        string appid = Regex.Match(badge.InnerHtml, @"card_drop_info_gamebadge_(\d+)_").Groups[1].Value;
                        HtmlNodeCollection row = badge.SelectNodes(".//span[contains(@class, 'progress_info_bold')]");
                        if (row != null)
                        {
                            foreach (HtmlNode data in row)
                            {
                                if (data != null)
                                {
                                    if (Regex.Match(data.InnerHtml, @"\d").Length > 0)
                                    {
                                        string remaining = Regex.Match(data.InnerHtml, @"(\d+)").Groups[1].Value;
                                        totaldrops = totaldrops + Convert.ToInt16(remaining);
                                        if (badgesLeft.ContainsKey(appid) == false)
                                        {
                                            Boolean onBlacklist = false; ;
                                            foreach (String blApp in Properties.Settings.Default.blacklist)
                                            {
                                                if (blApp == appid)
                                                {
                                                    onBlacklist = true;
                                                }
                                            }
                                            if (onBlacklist == false)
                                            {
                                                badgesLeft.Add(appid, remaining);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                } while (i < last);
            }
            catch (Exception)
            {
                // Only one page, we're good to go!
            }

            SortBadges(Properties.Settings.Default.sort);

            picReadingPage.Visible = false;
            lblIdle.Text = badgesLeft.Count + " games left to idle";
            lblIdle.Visible = true;
            lblDrops.Text = totaldrops.ToString() + " card drops remaining";
            lblDrops.Visible = true;
            
            // Set global variable values
            totalCardsRemaining = totaldrops;
            totalGamesRemaining = badgesLeft.Count;

            if (totaldrops == 0)
            {
                idleComplete();
            }
        }

        public async Task checkCardDrops(String appid)
        {
            CookieContainer cookies = generateCookies();
            string response = await GetHttpAsync(cookie_profileURL + "/gamecards/" + appid + "/", cookies);
            HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();
            document.LoadHtml(response);
            HtmlNodeCollection drops = document.DocumentNode.SelectNodes("//span[contains(@class,'progress_info_bold')]");
            try
            {
                String numDrops = drops[0].InnerText;
                lblCurrentRemaining.Text = numDrops;
                int intDrops;
                if (Int32.TryParse(Regex.Match(numDrops, @"(\d+)").Groups[1].Value, out intDrops)) {
                    // card drops remaining
                    Console.WriteLine(GetAppName(appid) + " has " + intDrops + " card drops remaining.");

                    // Determine if the drop count has changed
                    int dropsSoFar = Int32.Parse(badgesLeft[appid]) - intDrops;
                    int dropsBefore = pbIdle.Value;

                    if (dropsBefore != dropsSoFar)
                    {
                        totalCardsRemaining = totalCardsRemaining - (dropsSoFar - dropsBefore);
                        lblDrops.Text = totalCardsRemaining + " card drops remaining";
                        pbIdle.Value = dropsSoFar;
                    }

                    // Resets the clock based on the number of remaining drops
                    if (intDrops == 1)
                    {
                        timeLeft = 300;
                    }
                    else
                    {
                        timeLeft = 900;
                    }
                    
                }
                else
                {
                    // no card drops remaining
                    Console.WriteLine(GetAppName(appid) + " has no card drops remaining.");

                    badgesLeft.Remove(appid);

                    // Update totals
                    totalCardsRemaining = totalCardsRemaining - 1;
                    totalGamesRemaining = totalGamesRemaining - 1;
                    lblIdle.Text = totalGamesRemaining + " games left to idle";
                    lblDrops.Text = totalCardsRemaining + " card drops remaining";

                    // Stop idling the current game
                    stopIdle();

                    if (badgesLeft.Count != 0)
                    {
                        startIdle(badgesLeft.First().Key);
                    }
                    else
                    {
                        idleComplete();
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public async Task GetLoginDetails()
        {
            CookieContainer cookies_prep = new CookieContainer();
            try
            {
                // Get a list of the system's running processes
                Win32Process.SYSTEM_INFO sysInfo = default(Win32Process.SYSTEM_INFO);
                while (sysInfo.minimumApplicationAddress.ToInt32() == 0)
                {
                    Win32Process.GetSystemInfo(out sysInfo);
                }

                // Find the exact address of the Steam process
                IntPtr minAddress = sysInfo.minimumApplicationAddress;
                long num = (long)minAddress.ToInt32();
                List<string> list = new List<string>();
                Process[] processes = Process.GetProcessesByName("steam");
                Process process = null;
                for (int i = 0; i < processes.Length; i++)
                {
                    foreach (ProcessModule processModule in processes[i].Modules)
                    {
                        if (processModule.FileName.EndsWith("steamclient.dll"))
                        {
                            process = processes[i];
                            break;
                        }
                    }
                }
                if (process != null)
                {
                    // Get the Steam process handle
                    IntPtr process_handle = Win32Process.OpenProcess(1040u, false, process.Id);
                    Win32Process.PROCESS_QUERY_INFORMATION process_query = default(Win32Process.PROCESS_QUERY_INFORMATION);
                    IntPtr intPtr = new IntPtr(0);
                    while (Win32Process.VirtualQueryEx(process_handle, minAddress, out process_query, 28u) != 0)
                    {
                        if (process_query.Protect == 4u && process_query.State == 4096u)
                        {
                            // Read the Steam process memory stream to extract the steamLogin cookie data
                            byte[] process_query_size = new byte[process_query.RegionSize];
                            Win32Process.ReadProcessMemory(process_handle, process_query.BaseAdress, process_query_size, process_query.RegionSize, out intPtr);
                            string process_memory_string = Encoding.UTF8.GetString(process_query_size);
                            MatchCollection matches = new Regex("7656119[0-9]{10}%7c%7c[A-F0-9]{40}", RegexOptions.IgnoreCase).Matches(process_memory_string);
                            if (matches.Count > 0)
                            {
                                foreach (Match match in matches)
                                {
                                    if (!list.Contains(match.Value))
                                    {
                                        list.Add(match.Value);
                                    }
                                }
                            }
                        }
                        num += (long)((ulong)process_query.RegionSize);
                        if (num >= 2147483647L)
                        {
                            break;
                        }
                        minAddress = new IntPtr(num);
                    }
                }
                cookie_steamLogin = list[0];
                cookie_profileURL = "http://steamcommunity.com/profiles/" + cookie_steamLogin.Substring(0, 17);

                // Now that we have the steamLogin cookie data, perform a web query and extract the sessionid
                Uri uri = new Uri("http://steamcommunity.com/");
                cookies_prep.Add(new Cookie("steamLogin", cookie_steamLogin) { Domain = uri.Host });
                await GetHttpAsync("http://steamcommunity.com/", cookies_prep);
                CookieCollection cookies = cookies_prep.GetCookies(new Uri("http://steamcommunity.com/"));
                for (int i = 0; i < cookies.Count; i++)
                {
                    string cookie_name = cookies[i].Name.ToString();
                    if (cookie_name == "sessionid")
                    {
                        cookie_sessionid = cookies[i].Value.ToString();
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        public frmMain()
        {            
            InitializeComponent();
        }

        private async void frmMain_Load(object sender, EventArgs e)
        {
            // Copy external references to the output directory.  This allows ClickOnce install.
            if (File.Exists(Environment.CurrentDirectory + "\\steam_api.dll") == false)
            {
                CopyResource("IdleMaster.Resources.steam_api.dll", Environment.CurrentDirectory + @"\steam_api.dll");
            }
            if (File.Exists(Environment.CurrentDirectory + "\\CSteamworks.dll") == false)
            {
                CopyResource("IdleMaster.Resources.CSteamworks.dll", Environment.CurrentDirectory + @"\CSteamworks.dll");
            }
            if (File.Exists(Environment.CurrentDirectory + "\\steam-idle.exe") == false)
            {
                CopyResource("IdleMaster.Resources.steam-idle.exe", Environment.CurrentDirectory + @"\steam-idle.exe");
            }

            // Update the settings, if needed.  When the application updates, settings will persist.
            if (Properties.Settings.Default.updateNeeded)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.updateNeeded = false;
                Properties.Settings.Default.Save();
            }

            // Set the form height
            Graphics graphics = this.CreateGraphics();
            double scale = graphics.DpiY * 1.625;
            this.Height = Convert.ToInt32(scale);

            // Set the location of certain elements so that they scale correctly for different DPI settings
            double lblGameName_scale = graphics.DpiX * 1.14;
            double lnkRetry_scale = graphics.DpiX * 2.29;
            Point point = new Point(Convert.ToInt32(lblGameName_scale), Convert.ToInt32(lblGameName.Location.Y));
            lblGameName.Location = point;
            point = new Point(Convert.ToInt32(lnkRetry_scale), Convert.ToInt32(lnkRetry.Location.Y));
            lnkRetry.Location = point;

            // Get the user information of the currently logged in Steam user
            await GetLoginDetails();
            tmrCheckCookieData.Enabled = true;
        }

        private void frmMain_FormClose(object sender, FormClosedEventArgs e)
        {
            try 
            {
                stopIdle();
            }
            catch (Exception)
            {

            }
        }

        private void tmrCheckCookieData_Tick(object sender, EventArgs e)
        {
            if (cookie_sessionid != "" && cookie_steamLogin != "")
            {
                lblCookieStatus.Text = "Idle Master is connected to Steam";
                lblCookieStatus.ForeColor = System.Drawing.Color.Green;
                picCookieStatus.Image = Properties.Resources.imgTrue;
                cookieReady = true;
                lnkRetry.Visible = false;
            }
            else
            {
                lblCookieStatus.Text = "Idle Master could not connect to Steam";
                lblCookieStatus.ForeColor = System.Drawing.Color.Black;
                picCookieStatus.Image = Properties.Resources.imgFalse;
                cookieReady = false;
                lnkRetry.Visible = true;
            }
        }

        private void tmrCheckSteam_Tick(object sender, EventArgs e)
        {
            if (SteamAPI.IsSteamRunning() == true) {
                lblSteamStatus.Text = "Steam is running";
                lblSteamStatus.ForeColor = System.Drawing.Color.Green;
                picSteamStatus.Image = Properties.Resources.imgTrue;
                tmrCheckSteam.Interval = 2500;                
                skipGameToolStripMenuItem.Enabled = true;
                pauseIdlingToolStripMenuItem.Enabled = true;
                steamReady = true;
            }
            else
            {
                // Alter the GUI
                lblSteamStatus.Text = "Steam is not running";
                lblSteamStatus.ForeColor = System.Drawing.Color.Black;
                picSteamStatus.Image = Properties.Resources.imgFalse;
                skipGameToolStripMenuItem.Enabled = false;
                pauseIdlingToolStripMenuItem.Enabled = false;
                picIdleStatus.Image = null;

                // Set the timer interval
                tmrCheckSteam.Interval = 500;
                
                // Stop Idling
                if (Idle != null)
                {
                    stopIdle();
                }

                // Reset the user data
                steamReady = false;
                cookieReady = false;
                cookie_sessionid = "";
                cookie_steamLogin = "";
                cookie_profileURL = "";

                // Resize the form
                Graphics graphics = this.CreateGraphics();
                double scale = graphics.DpiY * 1.625;
                this.Height = Convert.ToInt32(scale);

                // Hide lblDrops and lblIdle
                lblDrops.Visible = false;
                lblIdle.Visible = false;
                
                // Re-enable tmrReadyToGo
                tmrReadyToGo.Enabled = true;
                picReadingPage.Visible = false;
            }
        }

        private void lblGameName_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://store.steampowered.com/app/" + currentAppID);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void tmrReadyToGo_Tick(object sender, EventArgs e)
        {
            if (cookieReady && steamReady)
            {
                // Update the form elements
                lblDrops.Visible = true;
                lblDrops.Text = "Reading badge page, please wait...";
                lblIdle.Visible = false;
                picReadingPage.Visible = true;

                tmrReadyToGo.Enabled = false;

                // Call the loadBadges() function asynchronously
                await LoadBadgesAsync();

                if (badgesLeft.Count != 0)
                {
                    startIdle(badgesLeft.First().Key);
                }
                else
                {
                    idleComplete();
                }
            }
        }

        private async void tmrCardDropCheck_Tick(object sender, EventArgs e)
        {
            if (timeLeft > 0)
            {
                timeLeft = timeLeft - 1;
                int minutes = timeLeft / 60;
                int seconds = timeLeft - (minutes * 60);
                if (seconds < 10)
                {
                    lblTimer.Text = minutes + ":0" + seconds;
                }
                else
                {
                    lblTimer.Text = minutes + ":" + seconds;
                }
            }
            else
            {
                tmrCardDropCheck.Enabled = false;
                await checkCardDrops(currentAppID);
                if (badgesLeft.Count != 0)
                {
                    tmrCardDropCheck.Enabled = true;
                }
            }
        }

        private void btnSkip_Click(object sender, EventArgs e)
        {
          if (steamReady)
          {
            badgesLeft.Remove(currentAppID);
            stopIdle();
            if (badgesLeft.Count != 0)
            {
              startIdle(badgesLeft.First().Key);
            }
            else
            {
              idleComplete();
            }
          }
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
          if (steamReady)
          {
            // Stop the steam-idle process
            stopIdle();

            // Indicate to the user that idling has been paused
            lblCurrentStatus.Text = "Idling paused";

            // Set the correct button visibility
            btnResume.Visible = true;
            btnPause.Visible = false;
            pauseIdlingToolStripMenuItem.Enabled = false;
            resumeIdlingToolStripMenuItem.Enabled = true;

            // Focus the resume button
            btnResume.Focus();
          }
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            // Resume idling
            if (badgesLeft.Count != 0)
            {
                startIdle(currentAppID);
            }
            else
            {
                idleComplete();
            }

            pauseIdlingToolStripMenuItem.Enabled = true;
            resumeIdlingToolStripMenuItem.Enabled = false;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the form
            String previous = Properties.Settings.Default.sort;
            frmSettings frm = new frmSettings();
            frm.ShowDialog();

            if (previous != Properties.Settings.Default.sort)
            {
                stopIdle();
                badgesLeft.Clear();
                tmrReadyToGo.Enabled = true;
            }
        }

        private void pauseIdlingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnPause.PerformClick();
        }

        private void resumeIdlingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnResume.PerformClick();
        }

        private void skipGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnSkip.PerformClick();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmAbout frm = new frmAbout();
            frm.ShowDialog();
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                if (Properties.Settings.Default.minToTray == true)
                {
                    notifyIcon1.Visible = true;
                    this.Hide();
                }
            }
            else if (this.WindowState == FormWindowState.Normal)
            {
                notifyIcon1.Visible = false;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            timeLeft = 3;
        }

        private void blacklistToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmBlacklist frm = new frmBlacklist();
            frm.ShowDialog();

            foreach (String appid in Properties.Settings.Default.blacklist)
            {   
                if (appid == currentAppID)
                {
                    btnSkip.PerformClick();
                }
            }
        }

        private async void lnkRetry_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Get the user information of the currently logged in Steam user
            tmrCheckCookieData.Enabled = false;
            await GetLoginDetails();
            tmrCheckCookieData.Enabled = true;
        }
    }
}