using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PhantomGo.Services;
using PhantomGo.Models;
using PhantomGo.Core.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PhantomGo;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StartWindow : Window
{
    private bool _isEventMode = false;
    public StartWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1130, 800));

        IsEventRadio.IsChecked = false;
        IsPracticeRadio.IsChecked = true;

        InitializeAgentComboBox();
    }

    private void IsEventRadio_Checked(object sender, RoutedEventArgs e)
    {
        EventInfoPanel.Visibility = Visibility.Visible;
        _isEventMode = true;
    }

    private void IsPracticeRadio_Checked(object sender, RoutedEventArgs e)
    {
        EventInfoPanel.Visibility = Visibility.Collapsed;
        _isEventMode = false;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        // 保存信息
        if(!string.IsNullOrEmpty(BlackTeamNameBox.Text))
        {
            GameInfoService.Instance.BlackTeamName = BlackTeamNameBox.Text;
        }
        if(!string.IsNullOrEmpty(WhiteTeamNameBox.Text))
        {
            GameInfoService.Instance.WhiteTeamName = WhiteTeamNameBox.Text;
        }
        GameInfoService.Instance.IsEventMode = _isEventMode;
        if (_isEventMode)
        {
            if(!string.IsNullOrEmpty(EventNameBox.Text))
            {
                GameInfoService.Instance.EventName = EventNameBox.Text;
            }
            if(!string.IsNullOrEmpty(EventLocationBox.Text))
            {
                GameInfoService.Instance.EventLocation = EventLocationBox.Text;
            }
            GameInfoService.Instance.EventDateTime = DateTime.Now;
        }

        var blackAgent = (AgentEnum)Enum.ToObject(typeof(AgentEnum), BlackTeamAgentComboBox.SelectedIndex);
        GameInfoService.Instance.BlackAgent = blackAgent.ToPlayerAgent(boardSize:9, playerColor:Player.Black);
        var whiteAgent = (AgentEnum)Enum.ToObject(typeof(AgentEnum), WhiteTeamAgentComboBox.SelectedIndex);
        GameInfoService.Instance.WhiteAgent = whiteAgent.ToPlayerAgent(boardSize:9, playerColor:Player.White);

        // 跳转到游戏窗口
        var gameWindow = new MainWindow();
        gameWindow.Activate();
        this.Close();
    }
    private void InitializeAgentComboBox()
    {
        foreach(var agent in Enum.GetValues(typeof(AgentEnum)))
        {
            BlackTeamAgentComboBox.Items.Add(agent.ToString());
            WhiteTeamAgentComboBox.Items.Add(agent.ToString());
        }
    }
}
