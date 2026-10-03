using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

using Octokit;
using Octokit.Helpers;
using Octokit.Internal;

using Telegram.Bot.Types;

using static AITool.AITOOL;

namespace AITool
{
    public partial class Frm_UpdateCheck:Form
    {
        private DateTime CurrentVerTime = DateTime.MinValue;
        private GitHubClient client = null;
        private IReadOnlyList<Release> releases = null;
        private Release stableRelease = null;
        private Release betaRelease = null;

        private string RepoOwner => AppSettings.Settings.UpdateCheckRepository.Split('/')[0].Trim();
        private string RepoName => AppSettings.Settings.UpdateCheckRepository.Split('/').Last().Trim();

        public Frm_UpdateCheck()
        {
            InitializeComponent();
        }

        private void Frm_UpdateCheck_Load(object sender, EventArgs e)
        {
            Global_GUI.RestoreWindowState(this);



        }

        private void Frm_UpdateCheck_FormClosing(object sender, FormClosingEventArgs e)
        {
            Global_GUI.SaveWindowState(this);
        }


        private class ReleaseNote
        {
            public DateTime Date;
            public String Title = "";
            public String Body = "";
            public string Type = "";
            public string Version = "";
        }

        private async void bt_check_Click(object sender, EventArgs e)
        {

            try
            {

                this.ControlBox = false;

                Assembly CurAssm = Assembly.GetExecutingAssembly();
                string AssemVer = CurAssm.GetName().Version.Major + "." + CurAssm.GetName().Version.Minor + "." + CurAssm.GetName().Version.Build;
                CurrentVerTime = Global.RetrieveLinkerTimestamp();
                lbl_CurrentVersion.Text = $"{AssemVer} ({CurrentVerTime.ToShortDateString()})";

                List<ReleaseNote> notes = new List<ReleaseNote>();

                using Global_GUI.CursorWait cw = new Global_GUI.CursorWait();
                bt_check.Enabled = false;
                bt_check.Text = "Checking...";

                //first get the most recent release:
                if (client.IsNull())
                    client = new GitHubClient(new ProductHeaderValue("AITool"));

                Log("Getting Github rate limits...");

                var miscellaneousRateLimit = await client.Miscellaneous.GetRateLimits();

                //  The "core" object provides your rate limit status except for the Search API.
                var coreRateLimit = miscellaneousRateLimit.Resources.Core;

                var howManyCoreRequestsCanIMakePerHour = coreRateLimit?.Limit;
                var howManyCoreRequestsDoIHaveLeft = coreRateLimit?.Remaining;
                var whenDoesTheCoreLimitReset = coreRateLimit?.Reset.ToLocalTime(); // UTC time

                // the "search" object provides your rate limit status for the Search API.
                var searchRateLimit = miscellaneousRateLimit.Resources.Search;

                var howManySearchRequestsCanIMakePerMinute = searchRateLimit?.Limit;
                var howManySearchRequestsDoIHaveLeft = searchRateLimit?.Remaining;
                var whenDoesTheSearchLimitReset = searchRateLimit?.Reset.ToLocalTime(); // UTC time

                Log($"Github>>     Max Core Requests per hour: {howManyCoreRequestsCanIMakePerHour}  (remaining={howManyCoreRequestsDoIHaveLeft} left)");
                Log($"Github>> Max Search Requests per minute: {howManySearchRequestsCanIMakePerMinute}  (remaining={howManySearchRequestsDoIHaveLeft} left)");

                //------------------------------
                //Max Core=60/hr
                //Max search=10/min
                //WE USE ABOUT 20 CORE REQUESTS EACH UPDATE CHECK
                //doesnt look like we actually use "SEARCH" requests for this update check -Vorlon
                //------------------------------

                if (howManyCoreRequestsDoIHaveLeft <= 1 && whenDoesTheCoreLimitReset.IsNotNull())
                {
                    string err = $"GitHub>> Please wait until after {whenDoesTheSearchLimitReset} to check for updates again.\r\n\r\n(CORE Limit={howManyCoreRequestsCanIMakePerHour}/hr, remaining={howManyCoreRequestsDoIHaveLeft})";
                    Log(err);
                    MessageBox.Show(err, "GITHUB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (howManySearchRequestsDoIHaveLeft <= 1 && whenDoesTheSearchLimitReset.IsNotNull())
                {
                    string err = $"GitHub>> Please wait until after {whenDoesTheSearchLimitReset} to check for updates again.\r\n\r\n(SEARCH Limit={howManySearchRequestsCanIMakePerMinute}/min, remaining={howManySearchRequestsDoIHaveLeft})";
                    Log(err);
                    MessageBox.Show(err, "GITHUB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Log("Loading latest Github release...");

                releases = await client.Repository.Release.GetAll(RepoOwner, RepoName);

                // "Release" is the newest normal release; "Beta" is the newest of any kind, including pre-releases
                stableRelease = releases.FirstOrDefault(r => !r.Draft && !r.Prerelease);
                betaRelease = releases.FirstOrDefault(r => !r.Draft);

                if (betaRelease.IsNull())
                {
                    string msg = $"No releases have been published yet at https://github.com/{RepoOwner}/{RepoName}/releases";
                    Log("Debug: " + msg);
                    MessageBox.Show(msg, "No releases", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ReleaseNote rn;

                if (stableRelease.IsNotNull())
                {
                    linkLabelRelease.Text = FormatRelease(stableRelease);
                    notes.Add(ToReleaseNote(stableRelease, "Release"));
                }
                else
                {
                    linkLabelRelease.Text = "(none yet)";
                }

                linkLabelBeta.Text = FormatRelease(betaRelease);
                if (betaRelease != stableRelease)
                    notes.Add(ToReleaseNote(betaRelease, "Pre-release"));

                string ver = VersionFromTag(betaRelease.TagName);

                lbl_message.Visible = Version.TryParse(ver, out Version latestVer) && CurAssm.GetName().Version < latestVer;

                var commits = await client.Repository.Commit.GetAll(RepoOwner, RepoName);

                for (int i = 0; i < commits.Count; i++)
                {
                    if (commits[i].Commit.Author.Date > betaRelease.PublishedAt.GetValueOrDefault().LocalDateTime)
                    {
                        rn = new ReleaseNote();
                        rn.Date = commits[i].Commit.Author.Date.LocalDateTime;
                        rn.Title = !commits[i].Label.IsNull() ? rn.Title : "";
                        rn.Body = commits[i].Commit.Message;
                        bool hasdash = rn.Body.TrimStart().StartsWith("-");
                        bool hasstar = rn.Body.TrimStart().StartsWith("*");
                        if (!hasdash && !hasstar)
                            rn.Body = "* " + rn.Body.Trim();

                        if (i == 0)
                            rn.Version = ver;
                        else
                            rn.Version = "";

                        rn.Type = "Commit";
                        notes.Insert(0, rn);
                    }
                }


                //sort by date
                notes = notes.OrderByDescending((d) => d.Date).ToList();

                StringBuilder Markup = new StringBuilder();


                foreach (var note in notes)
                {
                    Markup.AppendLine($"{note.Version} ({note.Date}) {note.Title}");
                    Markup.AppendLine("");
                    Markup.AppendLine($"{note.Body}");
                    Markup.AppendLine("");
                    Markup.AppendLine("");
                }

                var html = Markdig.Markdown.ToHtml(Markup.ToString());


                webBrowser1.DocumentText = html;



                // get the download URL for this file on a specific branch
                //var file = await client.Repository.Content.GetAllContentsByRef(repo.Id, path, branch);

                bt_InstallBeta.Enabled = true;
                bt_installRelease.Enabled = stableRelease.IsNotNull();

                //Setup the versions
                //Version latestGitHubVersion = new Version(releases[0].TagName);
                //Version localVersion = new Version("X.X.X");


            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
                MessageBox.Show("Error: " + ex.Message, "GITHUB API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.ControlBox = true;
                bt_check.Enabled = true;
                bt_check.Text = "Check";
            }
        }

        private void linkLabelRelease_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShellLauncher.Open(stableRelease.IsNotNull() ? stableRelease.HtmlUrl : $"https://github.com/{RepoOwner}/{RepoName}/releases");
        }

        private void linkLabelBeta_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShellLauncher.Open(betaRelease.IsNotNull() ? betaRelease.HtmlUrl : $"https://github.com/{RepoOwner}/{RepoName}/releases");
        }

        private async void bt_installRelease_Click(object sender, EventArgs e)
        {
            await this.DownloadAndRunInstallerAsync(stableRelease, bt_installRelease);
        }

        private static string VersionFromTag(string Tag)
        {
            return Tag.Trim().TrimStart('v', 'V');
        }

        private static string FormatRelease(Release Rel)
        {
            return $"{Rel.TagName} ({Rel.PublishedAt.GetValueOrDefault().LocalDateTime.ToShortDateString()})";
        }

        private static ReleaseNote ToReleaseNote(Release Rel, string Type)
        {
            ReleaseNote rn = new ReleaseNote();
            rn.Date = Rel.PublishedAt.GetValueOrDefault().LocalDateTime;
            rn.Title = Rel.Name;
            rn.Body = Rel.Body;
            rn.Version = Rel.TagName;
            rn.Type = Type;
            return rn;
        }

        private async Task DownloadAndRunInstallerAsync(Release Rel, Button Btn)
        {
            try
            {
                using Global_GUI.CursorWait cw = new Global_GUI.CursorWait();

                Btn.Enabled = false;
                Btn.Text = "Working";

                ReleaseAsset asset = Rel?.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset.IsNull())
                {
                    MessageBox.Show($"Release '{Rel?.TagName}' has no installer attached.", "Error downloading", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // the app folder is often under Program Files and not writable, so download to our temp folder
                string filename = Path.Combine(Global.GetTempFolder(), asset.Name);

                if (!System.IO.File.Exists(filename))
                {
                    using System.Net.Http.HttpClient http = new System.Net.Http.HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("AITool");
                    byte[] bytes = await http.GetByteArrayAsync(asset.BrowserDownloadUrl);
                    System.IO.File.WriteAllBytes(filename, bytes);
                }

                ExploreFile(filename);
                ShellLauncher.Open(filename);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error downloading", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Btn.Enabled = true;
                Btn.Text = "Download";
            }
        }

        public bool ExploreFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return false;
            }
            //Clean up file path so it can be navigated OK
            filePath = System.IO.Path.GetFullPath(filePath);
            System.Diagnostics.Process.Start("explorer.exe", string.Format("/select,\"{0}\"", filePath));
            return true;
        }

        private async void bt_InstallBeta_Click(object sender, EventArgs e)
        {
            await this.DownloadAndRunInstallerAsync(betaRelease, bt_InstallBeta);
        }

        private void linkLabelReportIssue_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShellLauncher.Open($"https://github.com/{RepoOwner}/{RepoName}/issues");
        }

        private void linkLabelIPCam_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShellLauncher.Open("https://ipcamtalk.com/threads/tool-tutorial-free-ai-person-detection-for-blue-iris.37330/");
        }

        private void btn_Donate_Click(object sender, EventArgs e)
        {
            ShellLauncher.Open("https://github.com/sponsors/VorlonCD");
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {

        }

        private void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            //var doc = webBrowser1.Document;

            //if (doc != null)
            //{
            //    doc.ExecCommand("FontSize", false, 10);
            //    doc.ExecCommand("FontFamily", false, "consolas");
            //}
        }
    }
}
