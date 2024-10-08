﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace doujin_manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DBAccessor accessor;
        private bool isUserHavingControl = true;

        public MainWindow()
        {
            InitializeComponent();
            accessor = DBAccessor.GetDBAccessor();
            accessor.Initialize();

            refreshAllBookList();
            refreshArtistList();
            resetArtistInputBox();
        }

        // debug
        private static string dump(System.Collections.IList v)
        {
            if (v.Count == 0)
            {
                return "[empty array]";
            }
            string ret = "";
            for (int i = 0; i < v.Count; i++)
            {
                ret += string.Format("[{0}] {1}, ", i, v[i].ToString());
            }
            return ret.TrimEnd(new char[] { ' ', ',' });
        }

        private static void openDialogue(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void refreshAllBookList()
        {
            allbooks.ItemsSource = accessor.GetAllBooks();
            statusText.Text = string.Format("合計 {0} 冊 登録されています。", allbooks.Items.Count);
        }

        private void refreshArtistList()
        {
            List<ArtistModel> artists = accessor.GetAllArtists();
            Dictionary<int, int> counts = accessor.GetBookCountOfArtist();

            List<Tuple<ArtistModel, int>> items = artists.ConvertAll(a => new Tuple<ArtistModel, int>(a, counts.TryGetValue(a.Id, out int value) ? value : 0));
            artistList.ItemsSource = items;
            statusText.Text = string.Format("合計 {0} 人 登録されています。", artistList.Items.Count);
        }

        private void resetArtistInputBox()
        {
            artistInputBox.Children.Clear();
            artistInputBox.Children.Add(new ArtistInputControl(suggestArtist, artistCandidateSelectionChanged));

            buttonRemoveArtistInput.IsEnabled = false;
        }

        // event handlers
        private void tabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.OriginalSource is TabControl t))
            {
                return;
            }
            switch (t.SelectedIndex)
            {
                case 0:
                    refreshAllBookList();
                    break;
                case 1:
                    refreshArtistList();
                    break;
                case 2:
                    statusText.Text = "";
                    break;
            }
        }

        private void artistListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (artistList.SelectedItem == null)
            {
                booksOfArtist.ItemsSource = null;
                return;
            }
            ArtistModel ar = ((Tuple<ArtistModel, int>)artistList.SelectedItem).Item1;

            booksOfArtist.ItemsSource = accessor.GetBooksOfArtist(ar);
        }

        private void addArtistInput(object sender, RoutedEventArgs e)
        {
            ArtistInputControl control = new(suggestArtist, artistCandidateSelectionChanged);
            artistInputBox.Children.Add(control);
            buttonRemoveArtistInput.IsEnabled = true;
        }

        private void removeArtistInput(object sender, RoutedEventArgs e)
        {
            if (artistInputBox.Children.Count == 1)
            {
                return;
            }
            if (artistInputBox.Children.Count == 2)
            {
                buttonRemoveArtistInput.IsEnabled = false;
            }
            artistInputBox.Children.RemoveAt(artistInputBox.Children.Count - 1);
        }

        private void suggestArtist(object sender, RoutedEventArgs e)
        {
            ArtistInputControl aic = (ArtistInputControl)sender;
            string content = aic.Text;
            aic.ItemsSource = accessor.GetArtistsLike(content);
        }

        private void suggestCircle(object sender, RoutedEventArgs e)
        {
            string content = textCircle.Text;
            circleCandList.ItemsSource = accessor.GetCirclesLike(content);
        }

        private void artistCandidateSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ArtistInputControl aic = (ArtistInputControl)sender;
            ArtistModel ar = aic.SelectedItem;
            if (!isUserHavingControl || ar == null)
            {
                return;
            }
            isUserHavingControl = false;
            aic.IsChecked = false;
            int cur = circleCandList.SelectedValue == null ? -1 : (int)circleCandList.SelectedValue;

            List<CircleModel> circles = accessor.GetRelatedCircles(ar);
            if (circles.Count > 0)
            {
                circleCandList.ItemsSource = circles;
            }

            if (circles.Exists(c => c.Id == cur))
            {
                circleCandList.SelectedValue = cur;
            }
            isUserHavingControl = true;
        }

        private void circleCandidateSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CircleModel ci = (CircleModel)circleCandList.SelectedItem;
            if (!isUserHavingControl || ci == null)
            {
                return;
            }
            isUserHavingControl = false;
            checkNewCircle.IsChecked = false;
            List<ArtistModel> artists = accessor.GetRelatedArtists(ci);

            foreach (UIElement elem in artistInputBox.Children)
            {
                ArtistInputControl aic = (ArtistInputControl)elem;
                int cur = aic.SelectedValue;

                if (artists.Count > 0)
                {
                    aic.ItemsSource = artists;
                }

                if (artists.Exists(a => a.Id == cur))
                {
                    aic.SelectedValue = cur;
                }
            }
            isUserHavingControl = true;
        }

        private void registerNewBook(object sender, RoutedEventArgs e)
        {
            if (textTitle.Text.Length == 0)
            {
                openDialogue("タイトルを入力してください");
                return;
            }

            List<ArtistModel> alist = new();
            foreach (UIElement elem in artistInputBox.Children)
            {
                ArtistModel ar;
                ArtistInputControl aic = (ArtistInputControl)elem;
                if (aic.IsChecked)
                {
                    if (aic.Text.Length == 0)
                    {
                        openDialogue("作者名を入力してください");
                        return;
                    }
                    ar = new ArtistModel { Name = aic.Text };
                    int aid = accessor.InsertArtist(ar);
                    ar.Id = aid;
                }
                else
                {
                    ar = aic.SelectedItem;
                    if (ar == null)
                    {
                        openDialogue("作者を選択してください");
                        return;
                    }
                }
                alist.Add(ar);
            }

            CircleModel ci;
            if (checkNewCircle.IsChecked != null && (bool)checkNewCircle.IsChecked)
            {
                if (textCircle.Text.Length == 0)
                {
                    openDialogue("サークル名を入力してください");
                    return;
                }
                ci = new CircleModel { Name = textCircle.Text };
                int cid = accessor.InsertCircle(ci);
                ci.Id = cid;
            }
            else
            {
                ci = (CircleModel)circleCandList.SelectedItem;
                if (ci == null)
                {
                    openDialogue("サークルを選択してください");
                    return;
                }
            }
            DateTime? dt = pickDate.SelectedDate;
            DateOnly? d = null;
            if (dt != null)
            {
                DateTime dt_notnull = (DateTime)dt;
                d = new DateOnly(dt_notnull.Year, dt_notnull.Month, dt_notnull.Day);
            }
            BookModel b = new() { Title = textTitle.Text, Artists = alist, Circle = ci, Date = d };
            b.Id = accessor.InsertBook(b);
            statusText.Text = "登録に成功しました。";
            // clear fields
            textTitle.Clear();
            textCircle.Clear();
            pickDate.Text = "";
            circleCandList.ItemsSource = null;
            resetArtistInputBox();
        }
    }
}
