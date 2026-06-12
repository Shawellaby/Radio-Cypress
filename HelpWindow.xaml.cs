using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RadioCypress;

public partial class HelpWindow : Window
{
    public record StationListingItem(int Number, string Name);

    public IReadOnlyList<StationListingItem> Stations { get; }

    public string CopyrightYear => "2019 - " + DateTime.Now.Year.ToString();


    public HelpWindow(Dictionary<int, MainWindow.Station> stations)
    {
        InitializeComponent();

        Stations = stations
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new StationListingItem(kvp.Key, kvp.Value.Name))
            .ToList();

        DataContext = this;
        Loaded += (_, _) => Focus();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}