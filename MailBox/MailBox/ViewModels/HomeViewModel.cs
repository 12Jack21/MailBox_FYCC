﻿using MailBox.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using MailBox.Commands;
using System.Collections.ObjectModel;
using MailBox.Models;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.IO;
using MailBox.Services;
using System.Threading;

namespace MailBox.ViewModels
{
    class HomeViewModel:NotificationObject
    {
		private string title;
		public string Title
		{
			get { return title; }
			set
			{
				title = value;
				this.RaisePropertyChanged("Title");
			}
		}

		private object visibility;

		public object Visibility
		{
			get { return visibility; }
			set
			{
				visibility = value;
				this.RaisePropertyChanged("Visibility");
			}
		}

		private object content;

		public object Content
		{
			get { return content; }
			set
			{
				content = value;
				this.RaisePropertyChanged("Content");
			}
		}

		private bool isSnackActive;
		private string tipMessage;
		public bool IsSnackActive
		{
			get
			{
				return isSnackActive;
			}
			set
			{
				isSnackActive = value;
				RaisePropertyChanged("IsSnackActive");
			}
		}
		public string TipMessage
		{
			get
			{
				return tipMessage;
			}
			set
			{
				tipMessage = value;
				RaisePropertyChanged("TipMessage");
			}
		}

		/**
         * 账号信息结合
         */
		private ObservableCollection<AccountInfo> accountInfos = new ObservableCollection<AccountInfo>();

		public ObservableCollection<AccountInfo> AccountInfos
		{
			get { return accountInfos; }
			set
			{
				accountInfos = value;
				this.RaisePropertyChanged("AccountInfos");
			}
		}

		/**
         * 被选择的账号索引
         */
		private int accountSelectedIndex;

		public int AccountSelectedIndex
		{
			get { return accountSelectedIndex; }
			set
			{
				Console.WriteLine(value);
				accountSelectedIndex = value;
				this.RaisePropertyChanged("AccountSelected");
			}
		}

		public DelegateCommand NewMailCommand { get; set; }

		private void NewMail(object parameter)
		{
			if (Title == "写信") return;
			Title = "写信";
			Visibility = System.Windows.Visibility.Hidden;
			Content = new Frame
			{
				Content = new WriteMailController(AccountInfos[AccountSelectedIndex])
			};
		}

		//public DelegateCommand LogoutCommand { get; set; }

		//private void Logout(object parameter)
		//{
		//	Content = new Frame
		//	{
		//		Content = new WelcomeController(this)
		//	};
		//}

		public DelegateCommand ReceiveMailCommand { get; set; }

		// switch mail user function
		private void ReceiveMail(object parameter)
		{
			Title = "收件箱";
			Visibility = System.Windows.Visibility.Visible;
			AccountInfo a = AccountInfos[AccountSelectedIndex];
			MailUtil.LoginInfo info_pop3 = new MailUtil.LoginInfo()
			{
				account = a.Account,
				passwd = a.Password,
				site = a.PopHost
			};
			bool re = !MailUtil.validate_account_pop3(info_pop3);

			// for debug compare
			MailUtil.LoginInfo info_pop3_2 = new MailUtil.LoginInfo()
			{
				account = "alertdoll@163.com",
				passwd = "ybgissocute2020",
				site = "pop.163.com:110"
			};
			bool rere = !MailUtil.validate_account_pop3(info_pop3_2);

			// account is invaliad
			if (!re)
			{
				// show tip and remove invalid account item
				DialogHost.Show(new ShowInvalidController(), null, null);
				//DialogHost.CloseDialogCommand.Execute(null, null);
				Console.WriteLine("Account Invalid !");

				//  delete account item and remove that in XML file also
				XMLOperation.DeleteAccouts(a);
				AccountInfos.RemoveAt(AccountSelectedIndex);
				return;
			}
			Content = new Frame
			{
				Content = new ReceiveMailController(AccountInfos[AccountSelectedIndex]) // don't flush
			};
		}

		private string searchText;
		public string SearchText
		{
			get
			{
				return searchText;
			}
			set
			{
				searchText = value;
				SearchMail(value);
			}
		}
		private ReceiveMailViewModel rvm;
		public DelegateCommand SearchCommand { get; set; }
		private void SearchMail(object parameter)
		{
			if(rvm == null)
            {
                Frame f = (Frame)Content;
                rvm = (f.Content as ReceiveMailController).DataContext as ReceiveMailViewModel;
            }
            rvm.SearchMail(SearchText);
		}

		private void ClearUserDir()
		{
			string root_dir = Environment.CurrentDirectory; // temporary using /bin/Debug
			string user_dir = Path.Combine(root_dir, AccountInfos[AccountSelectedIndex].Account);
			if (Directory.Exists(user_dir))
				Directory.Delete(user_dir, true);
		}
		private void Save_Mail()
		{
			AccountInfo a = AccountInfos[AccountSelectedIndex];
			MailUtil.LoginInfo info_pop3 = new MailUtil.LoginInfo()
            {
                account = a.Account,
                passwd = a.Password,
                site = a.PopHost
            };
            try
            {
                int num = MailUtil.get_num_mails(info_pop3);
                //info_pop3.account = "11";
                Task[] tasks = new Task[num];
                for (uint i = 1; i <= num; i++)
                {
                    uint param = i;
                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;
                    tasks[i - 1] = WaitAsync(Task.Factory.StartNew(() =>
                    {
                        int r = MailUtil.pull_save_mail(info_pop3, param);
                        if (r != -1)
                            Console.WriteLine("Receive mail-{0} success", param);
                        else
                            Console.WriteLine("Receive mail-{0} fail", param);
                    }), TimeSpan.FromSeconds(4.5));
                }
                Task.WaitAll(tasks, TimeSpan.FromSeconds(5.0)); // wait for 5 seconds
                Console.WriteLine("tasks all completed");
            }
            catch (TimeoutException te)
            {
                Console.WriteLine("Timeout happened, msg:" + te.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
			}
		}
		async Task WaitAsync(Task task, TimeSpan timeout)
		{
			using (var timeoutCancellationTokenSource = new CancellationTokenSource())
			{
				var delayTask = Task.Delay(timeout, timeoutCancellationTokenSource.Token);
				if (await Task.WhenAny(task, delayTask) == task)
				{
					timeoutCancellationTokenSource.Cancel();
					await task;
				}
				else
					throw new TimeoutException("The operation has timed out.");
				//Console.WriteLine("timeout happened.");
			}
		}
		public DelegateCommand FreshCommand { get; set; }
		private async void FreshMail(object parameter)
		{
			ClearUserDir();
			ShowFreshDialog(parameter);
			await Task.Run(() =>
			{
				Save_Mail();
			});
			Visibility = System.Windows.Visibility.Visible;
			Content = new Frame
			{
				Content = new ReceiveMailController(AccountInfos[AccountSelectedIndex])
			};
			DialogHost.CloseDialogCommand.Execute(null, null);

			// show snackbar
			TipMessage = "刷新结束";
			IsSnackActive = true;
			await Task.Delay(3000);
			IsSnackActive = false;

		}
		private async void ShowFreshDialog(object parameter)
		{
			DialogOpenedEventHandler openedEventHandler = null;
			DialogClosingEventHandler closingEventHandler = null;
			Console.WriteLine("Parameter:", parameter);
			await DialogHost.Show(new FreshController(), openedEventHandler, closingEventHandler); //TODO add CancellationToken
		}

		public HomeViewModel(ObservableCollection<AccountInfo> accountInfos, int selectIndex)
		{
			AccountInfos = accountInfos;
			AccountSelectedIndex = selectIndex;
			title = "收件箱";
			visibility = System.Windows.Visibility.Visible;

			NewMailCommand = new DelegateCommand();
			NewMailCommand.ExecuteAction = new Action<object>(NewMail);
			ReceiveMailCommand = new DelegateCommand();
			ReceiveMailCommand.ExecuteAction = new Action<object>(ReceiveMail);
			FreshCommand = new DelegateCommand();
			FreshCommand.ExecuteAction = new Action<object>(FreshMail);
			SearchCommand = new DelegateCommand();
			SearchCommand.ExecuteAction = new Action<object>(SearchMail);

			if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, AccountInfos[AccountSelectedIndex].Account)))
				Task.Run(() =>
				{
					Save_Mail();
				}).Wait();

			Content = new Frame
			{
				Content = new ReceiveMailController(AccountInfos[AccountSelectedIndex])
			};
		}
	}
}
