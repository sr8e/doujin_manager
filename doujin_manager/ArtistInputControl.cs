using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace doujin_manager
{
    public class ArtistInputControl : Control
    {
        CheckBox isNewArtist;
        TextBox artistInput;
        ListBox artistCandList;

        public static readonly RoutedEvent TextChangedEvent = EventManager.RegisterRoutedEvent(
            "TextChanged", RoutingStrategy.Bubble, typeof(TextChangedEventHandler), typeof(ArtistInputControl)
        );
        public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "SelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(ArtistInputControl)
        );

        static ArtistInputControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ArtistInputControl), new FrameworkPropertyMetadata(typeof(ArtistInputControl)));
        }

        public ArtistInputControl(TextChangedEventHandler t, SelectionChangedEventHandler s)
        {
            TextChanged += t;
            SelectionChanged += s;
        }

        public override void OnApplyTemplate()
        {
            Debug.WriteLine("template applied");
            base.OnApplyTemplate();
            isNewArtist = (CheckBox)GetTemplateChild("checkNewArtist");
            artistInput = (TextBox)GetTemplateChild("textArtist");
            artistCandList = (ListBox)GetTemplateChild("listArtistCand");

            artistInput.TextChanged += (_, e) => RaiseEvent(new TextChangedEventArgs(TextChangedEvent, e.UndoAction));
            artistCandList.SelectionChanged += (_, e) => RaiseEvent(new SelectionChangedEventArgs(SelectionChangedEvent, e.RemovedItems, e.AddedItems));
        }

        public bool IsChecked
        {
            get { return isNewArtist.IsChecked ?? false; }
            set { isNewArtist.IsChecked = value; }
        }

        public string Text
        {
            get { return artistInput.Text; }
        }

        public List<ArtistModel> ItemsSource
        {
            get { return (List<ArtistModel>)artistCandList.ItemsSource; }
            set { artistCandList.ItemsSource = value; }
        }

        public ArtistModel SelectedItem
        {
            get { return (ArtistModel)artistCandList.SelectedItem; }
        }

        public int SelectedValue
        {
            get { return (int)(artistCandList.SelectedValue ?? -1); }
            set { artistCandList.SelectedValue = value; }
        }

        public void Clear()
        {
            artistInput.Clear();
            artistCandList.ItemsSource = null;
        }

        public event TextChangedEventHandler TextChanged
        {
            add { AddHandler(TextChangedEvent, value); }
            remove { RemoveHandler(TextChangedEvent, value); }
        }

        public event SelectionChangedEventHandler SelectionChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }
    }
}
