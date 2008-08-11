﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

using ClassLibrary.Business;

namespace SilverlightDataGridApp
{
  public partial class Page : UserControl
  {
    public Page()
    {
      InitializeComponent();
    }
    private bool _editing = false;
    RootSingleItemsList _currentList;
    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
      Csla.DataPortal.ProxyTypeName = "Csla.DataPortalClient.WcfProxy, Csla";
      Csla.DataPortalClient.WcfProxy.DefaultUrl = "http://localhost:1324/SilverlightDataGridAppWeb/WcfPortal.svc";
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
      LoginStatus.Text = "Logging in...";
      BusinessPrincipal.Login("SergeyB", "1234", "admin;user", (objectValue, eventArgs) =>
      {
        if (Csla.ApplicationContext.User.Identity.IsAuthenticated)
        {
          LoginStatus.Text = "Logged in as " + Csla.ApplicationContext.User.Identity.Name;
          ItemsGrid.Visibility = Visibility.Visible;
          LoginStatus.Text = "Getting items...";
          RootSingleItemsList.GetRootSingleItemsList(1, 100, (o, ef) =>
            {
              if (ef.Error != null)
              {
                LoginStatus.Text = "Error getting items list";
              }
              else
              {
                ItemsGrid.ItemsSource = ef.Object;
                LoginStatus.Text = "Got data.";
                _currentList = ef.Object;
                if (_currentList.Count > 0)
                  ItemsGrid.SelectedItem = _currentList[0];
              }
            });

        }
        else
        {
          LoginStatus.Text = "Login failed";
        }
      });
    }

    private void LoginFail_Click(object sender, RoutedEventArgs e)
    {
      LoginStatus.Text = "Logging in...";
      BusinessPrincipal.Login("admin", "pwd", "admin;user", (objectValue, eventArgs) =>
      {
        if (Csla.ApplicationContext.User.Identity.IsAuthenticated)
        {
          LoginStatus.Text = "Logged in as " + Csla.ApplicationContext.User.Identity.Name;
          ItemsGrid.Visibility = Visibility.Collapsed;
        }
        else
        {
          LoginStatus.Text = "Login failed";
        }
      });
    }


    private void ItemsGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
      _editing = true;
    }

    private void ItemsGrid_CancelingEdit(object sender, DataGridEndingEditEventArgs e)
    {
      _editing = false;
    }

    private void ItemsGrid_CommittingEdit(object sender, DataGridEndingEditEventArgs e)
    {
      _editing = false;
    }

    private void ItemsGrid_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Delete && !_editing)
      {
        if (ItemsGrid.SelectedItem != null)
        {
          int index = _currentList.IndexOf((SingleItem)ItemsGrid.SelectedItem);
          _currentList.RemoveAt(index);
        }
      }
    }

  }
}
