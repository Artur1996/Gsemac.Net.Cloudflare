﻿using Gsemac.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Gsemac.Net.Cloudflare.Iuam {

    public class FlareSolverrService :
        FlareSolverrServiceBase {

        // Public members

        public override bool Start(IFlareSolverrConfig config) {

            lock (mutex) {

                if (!processStarted) {

                    OnLog.Info("Starting FlareSolverr service");

                    this.config = config;

                    AddEnvironmentPath(config.FlareSolverrDirectoryPath);
                    AddEnvironmentPath(config.NodeJsDirectoryPath);

                    processStarted = DownloadFlareSolverr() &&
                        InstallFlareSolverr() &&
                        StartFlareSolverr();

                    if (!processStarted)
                        OnLog.Error("FlareSolverr process could not be started");

                }

            }

            return processStarted;

        }
        public override bool Stop() {

            lock (mutex) {

                if (processStarted) {

                    OnLog.Info("Stopping FlareSolverr service");

                    StopFlareSolverr();

                    processStarted = false;

                }

            }

            return processStarted;

        }

        // Protected members

        protected override void Dispose(bool disposing) {

            if (disposing) {

                lock (mutex)
                    StopFlareSolverr();

            }

        }

        // Private members

        private const string defaultFlareSolverrDirectoryPath = @"FlareSolverr/";

        private readonly object mutex = new object();
        private bool processStarted = false;
        private IFlareSolverrConfig config;
        private Process flareSolverrProcess;

        private string GetFlareSolverrDirectoryPath() {

            return string.IsNullOrWhiteSpace(config.FlareSolverrDirectoryPath) ? defaultFlareSolverrDirectoryPath : config.FlareSolverrDirectoryPath;

        }
        private bool IsFlareSolverrInstalled() {

            string packageJsonFilePath = Path.Combine(GetFlareSolverrDirectoryPath(), "package.json");
            string okFilePath = Path.Combine(GetFlareSolverrDirectoryPath(), "INSTALL_OK");

            return File.Exists(packageJsonFilePath) && File.Exists(okFilePath);

        }

        private bool DownloadFlareSolverr() {

            if (!config.AutoDownload || IsFlareSolverrInstalled())
                return true;

            // FlareSolverr does not exist, so we'll download it to the given directory.

            //using (WebClient webClient = (config.WebRequestFactory ?? new HttpWebRequestFactory()).ToWebClientFactory().Create()) {

            //    OnLog.Info($"Downloading latest release from {flareSolverrRepositoryUrl}");

            //    OnLog.Info($"Downloading Node.js");

            //}

            return true;

        }
        private bool InstallFlareSolverr() {

            if (config.AutoInstall && !IsFlareSolverrInstalled()) {

                OnLog.Info($"Installing FlareSolverr");

                if (ExecuteProcess("cmd", "/C npm install") == 0) {

                    OnLog.Info($"Building FlareSolverr");

                    if (ExecuteProcess("cmd", "/C npm run build") == 0) {

                        File.Create(Path.Combine(GetFlareSolverrDirectoryPath(), "INSTALL_OK"));

                        return true;

                    }
                    OnLog.Error($"Failed to build FlareSolverr");

                }
                else
                    OnLog.Error($"Failed to install FlareSolverr");

            }

            // If we get here, the install failed, or automatic installation is disabled.

            return !config.AutoInstall;

        }
        private bool StartFlareSolverr() {

            if (IsFlareSolverrInstalled()) {

                OnLog.Info($"Starting FlareSolverr process");

                flareSolverrProcess = CreateProcess("node", "./dist/index.js");

                bool success = flareSolverrProcess.Start();

                // Give the process some time to fail so we can detect if FlareSolverr failed to start.

                if (success) {

                    if (flareSolverrProcess.WaitForExit((int)TimeSpan.FromSeconds(1).TotalMilliseconds))
                        success = success && flareSolverrProcess.ExitCode == 0;

                }

                if (success)
                    OnLog.Info($"FlareSolverr is now listening on port {FlareSolverrUtilities.DefaultPort}");

                return success;

            }
            else
                OnLog.Error($"FlareSolverr is not installed");

            return false;

        }
        private void StopFlareSolverr() {

            if (processStarted && flareSolverrProcess != null) {

                OnLog.Info("Stopping FlareSolverr process");

                flareSolverrProcess.Kill();
                flareSolverrProcess.Dispose();

                flareSolverrProcess = null;

            }

        }

        private Process CreateProcess(string fileName, string arguments) {

            ProcessStartInfo processStartInfo = new ProcessStartInfo() {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = GetFlareSolverrDirectoryPath(),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            // Add the directory containing node.exe to PATH so that the install process is aware of it.

            List<string> probingPaths = new List<string>() {
                GetFlareSolverrDirectoryPath(),
            };

            if (!string.IsNullOrWhiteSpace(config.NodeJsDirectoryPath))
                probingPaths.Add(config.NodeJsDirectoryPath);

            string[] oldPathValue = new[] { Environment.GetEnvironmentVariable("PATH") ?? string.Empty };
            string newPathValue = string.Join(Path.PathSeparator.ToString(), oldPathValue.Concat(probingPaths));

            processStartInfo.EnvironmentVariables["PATH"] = newPathValue;

            Process process = new Process {
                StartInfo = processStartInfo
            };

            process.OutputDataReceived += (sender, e) => OnLog.Info(e.Data);
            process.ErrorDataReceived += (sender, e) => OnLog.Error(e.Data);

            return process;

        }
        private int ExecuteProcess(string fileName, string arguments) {

            using (Process process = CreateProcess(fileName, arguments)) {

                process.Start();

                process.WaitForExit();

                return process.ExitCode;

            }

        }

        private void AddEnvironmentPath(string path) {

            if (!string.IsNullOrWhiteSpace(path) && !Environment.GetEnvironmentVariable("PATH").Contains(path))
                EnvironmentUtilities.AddEnvironmentPath(path);

        }

    }

}