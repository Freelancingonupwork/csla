﻿//-----------------------------------------------------------------------
// <copyright file="DataAnnotationsTests.cs" company="Marimer LLC">
//     Copyright (c) Marimer LLC. All rights reserved.
//     Website: https://cslanet.com
// </copyright>
// <summary>no summary</summary>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Csla;
using Csla.Core.FieldManager;
using Csla.Rules;
using Csla.Serialization;
using UnitDriven;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Csla.TestHelpers;

#if NUNIT
using NUnit.Framework;
using TestClass = NUnit.Framework.TestFixtureAttribute;
using TestInitialize = NUnit.Framework.SetUpAttribute;
using TestCleanup = NUnit.Framework.TearDownAttribute;
using TestMethod = NUnit.Framework.TestAttribute;
using TestSetup = NUnit.Framework.SetUpAttribute;
#elif MSTEST
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Csla.Test.DataAnnotations
{
#if TESTING
  [System.Diagnostics.DebuggerNonUserCode]
#endif
  [TestClass]
  public class DataAnnotationsTests : TestBase
  {
    private static TestDIContext _testDIContext;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
      _testDIContext = TestDIContextFactory.CreateDefaultContext();
    }

    [TestMethod]
    public async Task SingleAttribute()
    {
      var context = GetContext();

      var dp = _testDIContext.CreateDataPortal<Single>();
      var root = await dp.CreateAsync();
      var rules = root.GetRules();

      Assert.AreEqual(1, rules.Length, "Should be 1 rule");
      Assert.IsFalse(root.IsValid, "Obj shouldn't be valid");
      Assert.AreEqual(1, root.BrokenRulesCollection.Count, "Should be 1 broken rule");
      Assert.AreEqual("Name value required", root.BrokenRulesCollection[0].Description, "Desc should match");
      context.Assert.Success();

      context.Complete();
    }

    [TestMethod]
    public async Task MultipleAttributes()
    {
      var context = GetContext();

      var dp = _testDIContext.CreateDataPortal<Multiple>();
      var root = await dp.CreateAsync();
      var rules = root.GetRules();

      Assert.AreEqual(3, rules.Length, "Should be 3 rules");
      Assert.IsFalse(root.IsValid, "Obj shouldn't be valid");
      Assert.AreEqual(1, root.BrokenRulesCollection.Count, "Should be 1 broken rule");
      root.Name = "xyz";
      Assert.AreEqual(2, root.BrokenRulesCollection.Count, "Should be 2 broken rules after edit");
      context.Assert.Success();

      context.Complete();
    }

    [TestMethod]
    public async Task CustomAttribute()
    {
      var context = GetContext();

      var dp = _testDIContext.CreateDataPortal<Custom>();
      var root = await dp.CreateAsync();
      var rules = root.GetRules();

      Assert.AreEqual(1, rules.Length, "Should be 1 rule");
      Assert.IsFalse(root.IsValid, "Obj shouldn't be valid");
      Assert.AreEqual(1, root.BrokenRulesCollection.Count, "Should be 1 broken rule");
      Assert.AreEqual("Name must be abc", root.BrokenRulesCollection[0].Description, "Desc should match");
      context.Assert.Success();

      context.Complete();
    }

    [TestMethod]
    public void MultipleMetaRules()
    {
      IDataPortal<MultipleMeta> dataPortal = _testDIContext.CreateDataPortal<MultipleMeta>();

      var obj = dataPortal.Fetch();

      var typeRules = obj.GetRegisteredRules();
      // 4 rules from metadatacalss and 1 rule from autogenerated class. 
      Assert.AreEqual(5, typeRules.Rules.Count);
      Assert.AreEqual(3, typeRules.Rules.Where(p => p.PrimaryProperty == MultipleMeta.AmountProperty).Count());
      Assert.AreEqual(2, typeRules.Rules.Where(p => p.PrimaryProperty == MultipleMeta.QuantityProperty).Count());
    }
  }


  [Serializable]
  public class Single : BusinessBase<Single>
  {
    private static PropertyInfo<string> NameProperty = RegisterProperty<string>(c => c.Name);
    [Required(ErrorMessage = "Name value required")]
    public string Name
    {
      get { return GetProperty(NameProperty); }
      set { SetProperty(NameProperty, value); }
    }

    [Create]
    private async Task Create()
    {
      await BusinessRules.CheckRulesAsync();
    }

    public string[] GetRules()
    {
      return BusinessRules.GetRuleDescriptions();
    }
  }

  [Serializable]
  public class Multiple : BusinessBase<Multiple>
  {
    private static PropertyInfo<string> NameProperty = RegisterProperty<string>(c => c.Name);
    [Required(ErrorMessage = "Name value required")]
    [RegularExpression("[0-9]")]
    [System.ComponentModel.DataAnnotations.Range(typeof(string), "0", "9")]
    public string Name
    {
      get { return GetProperty(NameProperty); }
      set { SetProperty(NameProperty, value); }
    }

    [Create]
    private async Task Create()
    {
      await BusinessRules.CheckRulesAsync();
    }

    public string[] GetRules()
    {
      return BusinessRules.GetRuleDescriptions();
    }
  }

  [Serializable]
  public class Custom : BusinessBase<Custom>
  {
    private static PropertyInfo<string> NameProperty = RegisterProperty<string>(c => c.Name);
    [TestRule]
    public string Name
    {
      get { return GetProperty(NameProperty); }
      set { SetProperty(NameProperty, value); }
    }

    [Create]
    private async Task Create()
    {
      await BusinessRules.CheckRulesAsync();
    }

    public string[] GetRules()
    {
      return BusinessRules.GetRuleDescriptions();
    }
  }

  public class TestRuleAttribute : ValidationAttribute
  {
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
      if (validationContext.ObjectInstance == null)
        return new ValidationResult("ObjectInstance is null");
      var obj = validationContext.ObjectInstance as Custom;
      if (obj == null)
        return new ValidationResult("ObjectInstance is not the Custom type");
      if (string.IsNullOrEmpty(obj.Name) || obj.Name != "abc")
        return new ValidationResult("Name must be abc");
      return null;
    }
  }

  [Serializable()]
  public partial class MultipleMeta : Csla.BusinessBase<MultipleMeta>
  {
      public static PropertyInfo<decimal> AmountProperty = RegisterProperty<decimal>(p => p.Amount);

      [Required(ErrorMessage="Please enter an amount")]
      public decimal Amount
      {
          get { return GetProperty(AmountProperty); }
          set { SetProperty(AmountProperty, value); }
      }


      public static readonly PropertyInfo<int> QuantityProperty = RegisterProperty<int>(c => c.Quantity);
      public int Quantity
      { 
        get { return GetProperty(QuantityProperty); }
        set { SetProperty(QuantityProperty, value); }
      }

    [Fetch]
    private void Fetch()
    {
    }
  }

  public class MultipleMetaDataClass
  {
      [Required(ErrorMessage = "Amount is required")]
      [Range(typeof(decimal), "1", "100", ErrorMessage = "Please enter a value between 1 and 100")]
      public System.Decimal Amount { get; set; }

      [Required(ErrorMessage = "Quantity is required")]
      [Range(1, 100, ErrorMessage = "Please enter a value between 1 and 100")]
      public System.Int32 Quantity { get; set; }

  }
	  /// <summary>
  /// Non-Generate code that can be modified
  /// </summary>
  [MetadataType(typeof(MultipleMetaDataClass))]
  public partial class MultipleMeta : Csla.BusinessBase<MultipleMeta>
  {
    public new BusinessRuleManager GetRegisteredRules()
    {
      return base.GetRegisteredRules();
    }
  }
}