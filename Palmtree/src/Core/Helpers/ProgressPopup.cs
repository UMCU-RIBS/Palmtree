/**
 * The ProgressPopup class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2021:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Palmtree.Core.Helpers {
    
    /// <summary>
    /// The <c>ProgressPopup</c> class.
    /// </summary>
    public static class ProgressPopup {
        
        private static ProgressPopupForm progressPopup = null;
        private static Object popupLock = new Object();


        // show the popup modally and synchronously (this will claim the calling UI thread)
        //
        // title: title of the popup window
        // text: text underneath the progress bar (empty is no text)
        // width: window width in pixels (min 100 pixels)
        // allowCancel: adds a cancel option to the window and allows the user to close the window using the cross icon in the window bar
        // onCloseEvent = a close callback event; set to null for no callback
        // progression: 0 = manual updates of progression; > 0 timed progression in ms
        // closeAfterTime: 1 = dialog disappears after the progression time has passed; 0 = the prograss bar starts over at the end
        public static void showTimedPopup(string title, string text, int width, bool allowCancel, EventHandler<bool> onCloseEvent, int progression, bool closeAfterTime) {
            
            lock(popupLock) {

                // close previous
                if (progressPopup != null)
                    close(false, false);
            
                // open new
                progressPopup = new ProgressPopupForm(title, text, width, allowCancel, onCloseEvent, progression, closeAfterTime);

            }

            // 
            progressPopup.ShowDialog();

        }

        // re-use the popup asynchoronously (without claiming the thread that calls this function)
        // Note: used when a popup is re-used for multiple processes within a seperate non-UI thread
        // Note 2: the onCloseEvent of the popup that is re-used will not be called
        public static void reusePopup(bool fillFirst, string title, string text, int width, bool allowCancel, EventHandler<bool> onCloseEvent, int progression, bool closeAfterTime) {

            lock(popupLock) {

                if (progressPopup != null && progressPopup.IsHandleCreated && !progressPopup.IsDisposed) {

                    if (fillFirst) {
                        fillBar(true);
                        Thread.Sleep(100);
                    }
            
                    // set the layout, controls and timer
                    try {
                        progressPopup.Invoke((MethodInvoker)delegate {
                            progressPopup.setupForm(title, text, width, allowCancel, onCloseEvent, progression, closeAfterTime);
                        });
                    } catch (Exception) { }

                }
            }
        }
        
        public static void fillBar(bool stopTimer) {

            lock(popupLock) {
                if (progressPopup != null && progressPopup.IsHandleCreated && !progressPopup.IsDisposed) {

                    try {
                        progressPopup.Invoke((MethodInvoker)delegate {
                            progressPopup.fillBar(stopTimer);
                        });
                    } catch (Exception) { }

                }
            }

        }

        // close the popup
        // fillFirst: whether to show a full bar just before closing
        // cancelled: whether close is considered a cancel and - if present - the cancel callback is invoked
        public static void close(bool fillFirst, bool cancelled) {
            
            lock(popupLock) {

                if (progressPopup != null && progressPopup.IsHandleCreated && !progressPopup.IsDisposed) {

                    if (fillFirst) {
                        ProgressPopup.fillBar(true);
                        Thread.Sleep(100);
                    }
                        
                    // close the form
                    try {
                        progressPopup.Invoke((MethodInvoker)delegate {
                            progressPopup.DialogResult = cancelled ? DialogResult.Cancel : DialogResult.OK;
                            progressPopup.Close();
                        });
                    } catch (Exception) { }
                    progressPopup = null;

                }
                
            }
            
        }
    }


    /// <summary>
    /// The <c>ProgressPopup</c> class.
    /// </summary>
    public class ProgressPopupForm : Form {
        
        private EventHandler<bool> onCloseEvent = null;
        private int progression = 0;                    // 0 = manual updates of progression; > 0 timed progression in ms
        private bool closeAfterTime = true;

        private long startTime = 0;
        private double percElapsed;
        private System.Windows.Forms.Timer timer;
        private ProgressBar progressBar;
        private Label lblText;
        private Button btnCancel;

        // constructor
        // 
        // title: title of the popup window
        // text: text underneath the progress bar (empty is no text)
        // width: window width in pixels (min 100 pixels)
        // allowCancel: adds a cancel option to the window and allows the user to close the window using the cross icon in the window bar
        // onCloseEvent = a close callback event which passes whether it was cancelled or not as an argument; set to null for no callback
        // progression: 0 = manual updates of progression; > 0 timed progression in ms
        // closeAfterTime: 1 = dialog disappears after the progression time has passed; 0 = the prograss bar starts over at the end
        public ProgressPopupForm(string title, string text, int width, bool allowCancel, EventHandler<bool> onCloseEvent, int progression, bool closeAfterTime) {
            if (width < 200)    width = 200;
            
            progressBar = new ProgressBar();
            lblText = new Label();
            btnCancel = new Button();
            this.SuspendLayout();
            
            //
            progressBar.Location = new System.Drawing.Point(5, 5);
            progressBar.Name = "progressBar";
            progressBar.Size = new System.Drawing.Size(width - 10, 20);
            progressBar.TabIndex = 0;
            progressBar.Minimum = 0;
            progressBar.Maximum = 1000;

            lblText.Location = new System.Drawing.Point(5, 28);
            lblText.Name = "lblText";
            lblText.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, ((byte)(204)));
            lblText.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText.Size = new System.Drawing.Size(width - 10, 18);
            lblText.TabIndex = 2;
            lblText.Text = text;
            lblText.Visible = false;  // will be set later

            btnCancel.Location = new System.Drawing.Point(50, 50);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(70, 22);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += new System.EventHandler(delegate (object sender, EventArgs e) {
                this.DialogResult = DialogResult.Cancel;
            });
            btnCancel.Visible = false;  // will be set later


            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(width, 35);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblText);
            this.Controls.Add(btnCancel);
            if (onCloseEvent == null)
                this.ControlBox = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Name = "DlgProgress";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(delegate (object sender, FormClosingEventArgs e) {
                
                // stop the timer
                if (timer != null) {
                    timer.Stop();
                    timer = null;
                }
                                
                if (this.onCloseEvent != null)
                    this.onCloseEvent(this, this.DialogResult == DialogResult.Cancel);
                
            });

            this.ResumeLayout(false);


            // set the layout, controls and timer
            setupForm(title, text, width, allowCancel, onCloseEvent, progression, closeAfterTime);

        }
        public void setupForm(string title, string text, int width, bool allowCancel, EventHandler<bool> onCloseEvent, int progression, bool closeAfterTime) {
            if (width < 200)    width = 200;

            this.onCloseEvent = onCloseEvent;
            this.progression = progression;
            this.closeAfterTime = closeAfterTime;
        

            // set the form layout and controls
            this.SuspendLayout();
            
            // 
            this.Text = title;
            int formHeight = 35;
            int formY = 28;

            if (text != null && !string.IsNullOrEmpty(text)) {
                formHeight += 15;
                lblText.Location = new System.Drawing.Point(5, formY);
                lblText.Width = width - 10;
                lblText.Text = text;
                lblText.Visible = true;
                formY += lblText.Height;
            } else {
                lblText.Visible = false;
                formY += 5;
            }

            this.ControlBox = allowCancel;
            if (allowCancel) {
                formHeight += 25;
                btnCancel.Location = new System.Drawing.Point((width - btnCancel.Size.Width) / 2, formY);
                btnCancel.Visible = true;
            }

            progressBar.Size = new System.Drawing.Size(width - 10, 20);
            this.ClientSize = new System.Drawing.Size(width, formHeight);
            this.ResumeLayout(false);
            
            // 
            this.CenterToScreen();


            // 
            // timer
            // 

            // clear the timer if there is one
            if (timer != null) {
                timer.Stop();
                timer = null;
            }
            
            // if the progression is timed, set the interval and start the timer
            if (progression > 0) {
                timer = new System.Windows.Forms.Timer();

                // set an approprate timer update interval
                if (progression <= 50)              timer.Interval = progression;
                else if (progression <= 200)        timer.Interval = 50;
                else                                timer.Interval = 200;
                
                // 
                timer.Tick += (t, e) => {
                    
                    // calculate the elapsed time and determine what percentage of the progression-time has elapsed
                    long elapsed = Stopwatch.GetTimestamp() - startTime;
                    percElapsed = ((double)elapsed / Stopwatch.Frequency) / (progression / 1000);

                    // update the progress bar
                    int barValue = (int)(percElapsed * 1000);
                    if (barValue < 0)           barValue = 0;
                    if (barValue > 1000)        barValue = 1000;
                    try {
                        this.Invoke((MethodInvoker)delegate {
                            progressBar.Value = barValue;
                        });
                    } catch (Exception) { }

                    // after the progressed time-span
                    if (percElapsed >= 1) {

                        if (closeAfterTime) {
                            
                            // 
                            timer.Stop();

                            // close the form
                            try {
                                this.Invoke((MethodInvoker)delegate {
                                    this.DialogResult = DialogResult.OK;
                                    this.Close();
                                });
                            } catch (Exception) { }


                        } else {

                            // reset the starttime/progression
                            startTime = Stopwatch.GetTimestamp();

                        }
                        
                    }
                    
                };

                // start the timer
                startTime = Stopwatch.GetTimestamp();
                timer.Start();

            }

        }
        
        public void fillBar(bool stopTimer) {
            if (stopTimer)
                timer.Stop();
            percElapsed = 1;
            progressBar.Value = progressBar.Maximum;
        }
        
    }

}
