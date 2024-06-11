﻿//-----------------------------------------------------------------------
// <copyright file="BusinessRules.cs" company="Marimer LLC">
//     Copyright (c) Marimer LLC. All rights reserved.
//     Website: https://cslanet.com
// </copyright>
// <summary>Tracks the business rules for a business object.</summary>
//-----------------------------------------------------------------------

using System.Collections;
using Csla.Core;
using Csla.Serialization.Mobile;
using Csla.Server;
using Csla.Threading;

namespace Csla.Rules
{
  /// <summary>
  /// Tracks the business rules for a business object.
  /// </summary>
  [Serializable]
  public class BusinessRules :
    MobileObject, ISerializationNotification, IBusinessRules, IUseApplicationContext
  {
    /// <summary>
    /// Creates an instance of the type.
    /// </summary>
    public BusinessRules()
    { }

    /// <summary>
    /// Creates an instance of the type.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="target">Target business object.</param>
    public BusinessRules(ApplicationContext applicationContext, IHostRules target)
    {
      _applicationContext = applicationContext;
      SetTarget(target);
    }

    [NonSerialized]
    private object SyncRoot = new();

    ApplicationContext IUseApplicationContext.ApplicationContext { get => _applicationContext; set => _applicationContext = value; }
    private ApplicationContext _applicationContext;

    // list of broken rules for this business object.
    private BrokenRulesCollection _brokenRules;
    private BrokenRulesCollection BrokenRules
    {
      get
      {
        if (_brokenRules == null)
          _brokenRules = new BrokenRulesCollection(true);
        return _brokenRules;
      }
    }

    private bool _suppressRuleChecking;
    /// <summary>
    /// Gets or sets a value indicating whether calling
    /// CheckRules should result in rule
    /// methods being invoked.
    /// </summary>
    /// <value>True to suppress all rule method invocation.</value>
    public bool SuppressRuleChecking
    {
      get { return _suppressRuleChecking; }
      set { _suppressRuleChecking = value; }
    }

    private int _processThroughPriority;
    /// <summary>
    /// Gets or sets the priority through which
    /// all rules will be processed.
    /// </summary>
    public int ProcessThroughPriority
    {
      get { return _processThroughPriority; }
      set { _processThroughPriority = value; }
    }

    private string _ruleSet = null;
    /// <summary>
    /// Gets or sets the rule set to use for this
    /// business object instance.
    /// </summary>
    public string RuleSet
    {
      get { return string.IsNullOrEmpty(_ruleSet) ? ApplicationContext.DefaultRuleSet : _ruleSet; }
      set
      {
        _typeRules = null;
        _typeAuthRules = null;
        _ruleSet = value == ApplicationContext.DefaultRuleSet ? null : value;
        if (BrokenRules.Count > 0)
        {
          BrokenRules.ClearRules();
        }
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether rule engine should cacade n-leves when property value is changed from OuputPropertyValues. 
    /// </summary>
    /// <value>
    ///   <c>true</c> if [cascade when changed]; otherwise, <c>false</c>.
    /// </value>
    public bool CascadeOnDirtyProperties
    {
      get { return _cascadeOnDirtyProperties; }
      set { _cascadeOnDirtyProperties = value; }
    }


    [NonSerialized]
    private BusinessRuleManager _typeRules;
    internal BusinessRuleManager TypeRules
    {
      get
      {
        if (_typeRules == null && _target != null)
          _typeRules = BusinessRuleManager.GetRulesForType(_target.GetType(), _ruleSet);
        return _typeRules;
      }
    }

    [NonSerialized]
    private AuthorizationRuleManager _typeAuthRules;
    internal AuthorizationRuleManager TypeAuthRules
    {
      get
      {
        if (_typeAuthRules == null && _target != null)
          _typeAuthRules = AuthorizationRuleManager.GetRulesForType(_applicationContext, _target.GetType(), _ruleSet);
        return _typeAuthRules;
      }
    }

    /// <summary>
    /// Gets a list of rule:// URI values for
    /// the rules defined in the object.
    /// </summary>
    public string[] GetRuleDescriptions()
    {
      var result = new List<string>();
      foreach (var item in TypeRules.Rules)
        result.Add(item.RuleName);
      return result.ToArray();
    }

    // reference to current business object
    [NonSerialized]
    private IHostRules _target;

    internal void SetTarget(IHostRules target)
    {
      _target = target;
    }

    internal object Target
    {
      get { return _target; }
    }

    /// <summary>
    /// Associates a business rule with the business object.
    /// </summary>
    /// <param name="rule">Rule object.</param>
    public void AddRule(IBusinessRuleBase rule)
    {
      TypeRules.Rules.Add(rule);
    }

    /// <summary>
    /// Associates a business rule with the business object.
    /// </summary>
    /// <param name="rule">Rule object.</param>
    /// <param name="ruleSet">Rule set name.</param>
    public void AddRule(IBusinessRuleBase rule, string ruleSet)
    {
      var typeRules = BusinessRuleManager.GetRulesForType(_target.GetType(), ruleSet);
      typeRules.Rules.Add(rule);
    }

    /// <summary>
    /// Associates an authorization rule with the business object.
    /// </summary>
    /// <param name="rule">Rule object.</param>
    public void AddRule(IAuthorizationRuleBase rule)
    {
      EnsureUniqueRule(TypeAuthRules, rule);
      TypeAuthRules.Rules.Add(rule);
    }

    /// <summary>
    /// Associates a per-type authorization rule with 
    /// the business type in the default rule set.
    /// </summary>
    /// <param name="objectType">Type of business object.</param>
    /// <param name="rule">Rule object.</param>
    public static void AddRule(Type objectType, IAuthorizationRuleBase rule)
    {
      AddRule(objectType, rule, ApplicationContext.DefaultRuleSet);
    }

    /// <summary>
    /// Associates a per-type authorization rule with 
    /// the business type.
    /// </summary>
    /// <param name="objectType">Type of business object.</param>
    /// <param name="rule">Rule object.</param>
    /// <param name="ruleSet">Rule set name.</param>
    public static void AddRule(Type objectType, IAuthorizationRuleBase rule, string ruleSet)
    {
      AddRule(null, objectType, rule, ruleSet);
    }

    /// <summary>
    /// Associates a per-type authorization rule with 
    /// the business type.
    /// </summary>
    /// <param name="applicationContext">ApplicationContext instance</param>
    /// <param name="objectType">Type of business object.</param>
    /// <param name="rule">Rule object.</param>
    /// <param name="ruleSet">Rule set name.</param>
    public static void AddRule(ApplicationContext applicationContext, Type objectType, IAuthorizationRuleBase rule, string ruleSet)
    {
      var typeRules = AuthorizationRuleManager.GetRulesForType(applicationContext, objectType, ruleSet);
      EnsureUniqueRule(typeRules, rule);
      typeRules.Rules.Add(rule);
    }

    private static void EnsureUniqueRule(AuthorizationRuleManager mgr, IAuthorizationRuleBase rule)
    {
      IAuthorizationRuleBase oldRule = null;
      if (rule.Element != null)
        oldRule = mgr.Rules.FirstOrDefault(c => c.Element != null && c.Element.Name == rule.Element.Name && c.Action == rule.Action);
      else
        oldRule = mgr.Rules.FirstOrDefault(c => c.Element == null && c.Action == rule.Action);
      if (oldRule != null)
        throw new ArgumentException(nameof(rule));
    }

    /// <summary>
    /// Gets a value indicating whether there are
    /// any currently broken rules, which would
    /// mean the object is not valid.
    /// </summary>
    public bool IsValid
    {
      get { return BrokenRules.ErrorCount == 0; }
    }

    /// <summary>
    /// Gets the broken rules list.
    /// </summary>
    public BrokenRulesCollection GetBrokenRules()
    {
      return BrokenRules;
    }

    [NonSerialized]
    private bool _runningRules;
    /// <summary>
    /// Gets a value indicating whether a CheckRules
    /// operation is in progress.
    /// </summary>
    public bool RunningRules
    {
      get { return _runningRules; }
      private set { _runningRules = value; }
    }

    [NonSerialized]
    private bool _isBusy;

    [NonSerialized]
    private AsyncManualResetEvent _busyChanged;
    private AsyncManualResetEvent BusyChanged
    {
      get
      {
        if (_busyChanged == null)
          _busyChanged = new AsyncManualResetEvent();
        return _busyChanged;
      }
    }

    /// <summary>
    /// Gets a value indicating whether any async
    /// rules are currently executing.
    /// </summary>
    public bool RunningAsyncRules
    {
      get { return _isBusy; }
      set
      {
        _isBusy = value;
        if (_isBusy)
          BusyChanged.Reset();
        else
          BusyChanged.Set();
      }
    }

    /// <summary>
    /// Gets a value indicating whether a specific
    /// property has any async rules running.
    /// </summary>
    /// <param name="property">Property to check.</param>
    public bool GetPropertyBusy(IPropertyInfo property)
    {
      return BusyProperties.Contains(property);
    }

    /// <summary>
    /// Checks per-type authorization rules.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="action">Authorization action.</param>
    /// <param name="objectType">Type of business object.</param>
    public static bool HasPermission(ApplicationContext applicationContext, AuthorizationActions action, Type objectType)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));
      var activator = applicationContext.GetRequiredService<IDataPortalActivator>();
      var realType = activator.ResolveType(objectType);
      // no object specified so must use RuleSet from ApplicationContext
      return HasPermission(action, null, applicationContext, realType, null, applicationContext.RuleSet);
    }

    /// <summary>
    /// Checks per-type authorization rules.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="action">Authorization action.</param>
    /// <param name="objectType">Type of business object.</param>
    /// <param name="criteria">The criteria object provided.</param>
    public static bool HasPermission(ApplicationContext applicationContext, AuthorizationActions action, Type objectType, object[] criteria)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));
      var activator = applicationContext.GetRequiredService<IDataPortalActivator>();
      var realType = activator.ResolveType(objectType);
      // no object specified so must use RuleSet from ApplicationContext
      return HasPermission(action, null, applicationContext, realType, criteria, applicationContext.RuleSet);
    }

    /// <summary>
    /// Checks per-type authorization rules.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="action">Authorization action.</param>
    /// <param name="objectType">Type of business object.</param>
    /// <param name="ruleSet">The rule set.</param>
    /// <returns>
    /// 	<c>true</c> if the specified action has permission; otherwise, <c>false</c>.
    /// </returns>
    public static bool HasPermission(ApplicationContext applicationContext, AuthorizationActions action, Type objectType, string ruleSet)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));
      var activator = applicationContext.GetRequiredService<IDataPortalActivator>();
      var realType = activator.ResolveType(objectType);
      return HasPermission(action, null, applicationContext, realType, null, ruleSet);
    }

    /// <summary>
    /// Checks per-instance authorization rules.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="action">Authorization action.</param>
    /// <param name="obj">Business object instance.</param>
    public static bool HasPermission(ApplicationContext applicationContext, AuthorizationActions action, object obj)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));
      return HasPermission(action, obj, applicationContext, obj.GetType(), null, applicationContext.RuleSet);
    }

    private static bool HasPermission(AuthorizationActions action, object obj, ApplicationContext applicationContext, Type objType, object[] criteria, string ruleSet)
    {

      if (action == AuthorizationActions.ReadProperty ||
          action == AuthorizationActions.WriteProperty ||
          action == AuthorizationActions.ExecuteMethod)
        throw new ArgumentOutOfRangeException($"{nameof(action)}, {action}");

      bool result = true;
      var rule =
        AuthorizationRuleManager.GetRulesForType(applicationContext, objType, ruleSet).Rules.FirstOrDefault(c => c.Element == null && c.Action == action);
      if (rule != null)
      {
        if (rule is IAuthorizationRule sync)
        {
          var context = new AuthorizationContext(applicationContext, rule, obj, objType) { Criteria = criteria };
          sync.Execute(context);
          result = context.HasPermission;
        }
        else
          throw new ArgumentOutOfRangeException(rule.GetType().FullName);
      }
      return result;
    }

    /// <summary>
    /// Checks per-property authorization rules.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="action">Authorization action.</param>
    /// <param name="element">Property or method to check.</param>
    public bool HasPermission(ApplicationContext applicationContext, AuthorizationActions action, IMemberInfo element)
    {
      if (_suppressRuleChecking)
        return true;

      if (action == AuthorizationActions.CreateObject ||
          action == AuthorizationActions.DeleteObject ||
          action == AuthorizationActions.GetObject ||
          action == AuthorizationActions.EditObject)
        throw new ArgumentOutOfRangeException($"{nameof(action)}, {action}");

      bool result = true;
      var rule =
        TypeAuthRules.Rules.FirstOrDefault(c => c.Element != null && c.Element.Name == element.Name && c.Action == action);
      if (rule != null)
      {
        if (rule is IAuthorizationRule sync)
        {
          var context = new AuthorizationContext(applicationContext, rule, this.Target, this.Target.GetType());
          sync.Execute(context);
          result = context.HasPermission;
        }
        else
          throw new ArgumentOutOfRangeException(rule.GetType().FullName);
      }
      return result;
    }

    /// <summary>
    /// Checks per-type authorization rules.
    /// </summary>
    /// <param name="applicationContext">The application context.</param>
    /// <param name="action">The authorization action.</param>
    /// <param name="objectType">The type of the business object.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns a boolean indicating whether the permission is granted.</returns>
    public static Task<bool> HasPermissionAsync(ApplicationContext applicationContext, AuthorizationActions action, Type objectType, CancellationToken ct)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));

      var activator = applicationContext.GetRequiredService<IDataPortalActivator>();
      var realType = activator.ResolveType(objectType);

      // no object specified so must use RuleSet from ApplicationContext
      return HasPermissionAsync(action, null, applicationContext, realType, null, applicationContext.RuleSet, ct);
    }

    /// <summary>
    /// Checks per-type authorization rules.
    /// </summary>
    /// <param name="applicationContext">The application context.</param>
    /// <param name="action">The authorization action.</param>
    /// <param name="objectType">The type of the business object.</param>
    /// <param name="criteria">The criteria object provided.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a boolean value indicating whether the permission is granted.</returns>
    public static Task<bool> HasPermissionAsync(ApplicationContext applicationContext, AuthorizationActions action, Type objectType, object[] criteria, CancellationToken ct)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));

      var activator = applicationContext.GetRequiredService<IDataPortalActivator>();
      var realType = activator.ResolveType(objectType);

      // no object specified so must use RuleSet from ApplicationContext
      return HasPermissionAsync(action, null, applicationContext, realType, criteria, applicationContext.RuleSet, ct);
    }

    /// <summary>
    /// Checks per-type authorization rules.
    /// </summary>
    /// <param name="applicationContext">The application context.</param>
    /// <param name="action">Authorization action.</param>
    /// <param name="objectType">Type of business object.</param>
    /// <param name="ruleSet">The rule set.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// 	<c>true</c> if the specified action has permission; otherwise, <c>false</c>.
    /// </returns>
    public static Task<bool> HasPermissionAsync(ApplicationContext applicationContext, AuthorizationActions action, Type objectType, string ruleSet, CancellationToken ct)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));
      var activator = applicationContext.GetRequiredService<IDataPortalActivator>();
      var realType = activator.ResolveType(objectType);
      return HasPermissionAsync(action, null, applicationContext, realType, null, ruleSet, ct);
    }

    /// <summary>
    /// Checks per-instance authorization rules.
    /// </summary>
    /// <param name="applicationContext">The application context.</param>
    /// <param name="action">The authorization action.</param>
    /// <param name="obj">The business object instance.</param>
    /// <param name="ct">The cancellation token.</param>
    public static Task<bool> HasPermissionAsync(ApplicationContext applicationContext, AuthorizationActions action, object obj, CancellationToken ct)
    {
      if (applicationContext == null)
        throw new ArgumentNullException(nameof(applicationContext));
      return HasPermissionAsync(action, obj, applicationContext, obj.GetType(), null, applicationContext.RuleSet, ct);
    }

    private static async Task<bool> HasPermissionAsync(AuthorizationActions action, object obj, ApplicationContext applicationContext, Type objType, object[] criteria, string ruleSet, CancellationToken ct)
    {

      if (action == AuthorizationActions.ReadProperty ||
          action == AuthorizationActions.WriteProperty ||
          action == AuthorizationActions.ExecuteMethod)
        throw new ArgumentOutOfRangeException($"{nameof(action)}, {action}");

      bool result = true;
      var rule =
        AuthorizationRuleManager.GetRulesForType(applicationContext, objType, ruleSet).Rules.FirstOrDefault(c => c.Element == null && c.Action == action);
      if (rule != null)
      {
        if (rule is IAuthorizationRule sync)
        {
          var context = new AuthorizationContext(applicationContext, rule, obj, objType) { Criteria = criteria };
          sync.Execute(context);
          result = context.HasPermission;
        }
        else if (rule is IAuthorizationRuleAsync nsync)
        {
          var context = new AuthorizationContext(applicationContext, rule, obj, objType) { Criteria = criteria };
          await nsync.ExecuteAsync(context, ct);
          result = context.HasPermission;
        }
        else
          throw new ArgumentOutOfRangeException(rule.GetType().FullName);
      }
      return result;
    }

    /// <summary>
    /// Checks per-property authorization rules.
    /// </summary>
    /// <param name="applicationContext">The application context.</param>
    /// <param name="action">The authorization action.</param>
    /// <param name="element">The property or method to check.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a boolean value indicating whether the permission is granted.</returns>
    public async Task<bool> HasPermissionAsync(ApplicationContext applicationContext, AuthorizationActions action, Csla.Core.IMemberInfo element, CancellationToken ct)
    {
      if (_suppressRuleChecking)
        return true;

      if (action == AuthorizationActions.CreateObject ||
          action == AuthorizationActions.DeleteObject ||
          action == AuthorizationActions.GetObject ||
          action == AuthorizationActions.EditObject)
        throw new ArgumentOutOfRangeException($"{nameof(action)}, {action}");

      bool result = true;
      var rule =
          TypeAuthRules.Rules.FirstOrDefault(c => c.Element != null && c.Element.Name == element.Name && c.Action == action);
      if (rule != null)
      {
        if (rule is IAuthorizationRule sync)
        {
          var context = new AuthorizationContext(applicationContext, rule, this.Target, this.Target.GetType());
          sync.Execute(context);
          result = context.HasPermission;
        }
        else if (rule is IAuthorizationRuleAsync nsync)
        {
          var context = new AuthorizationContext(applicationContext, rule, this.Target, this.Target.GetType());
          await nsync.ExecuteAsync(context, ct);
          result = context.HasPermission;
        }
        else
          throw new ArgumentOutOfRangeException(rule.GetType().FullName);
      }
      return result;
    }

    /// <summary>
    /// Gets a value indicating whether the permission
    /// result can be cached.
    /// </summary>
    /// <param name="action">Authorization action.</param>
    /// <param name="element">Property or method to check.</param>
    public bool CachePermissionResult(AuthorizationActions action, IMemberInfo element)
    {
      // cannot cache result when suppressRuleChecking as HasPermission is then short circuited to return true.
      if (_suppressRuleChecking)
        return false;

      bool result = true;
      var rule =
        TypeAuthRules.Rules.FirstOrDefault(c => c.Element != null && c.Element.Name == element.Name && c.Action == action);
      if (rule != null)
        result = rule.CacheResult;
      return result;
    }

    /// <summary>
    /// Invokes all rules for a specific property of the business type.
    /// </summary>
    /// <param name="property">Property to check.</param>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">If property is null</exception>
    public Task<List<string>> CheckRulesAsync(IPropertyInfo property)
    {
      return CheckRulesAsync(property, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Invokes all rules for a specific property of the business type.
    /// </summary>
    /// <param name="property">Property to check.</param>
    /// <param name="timeout">Timeout to wait for the rule completion.</param>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">If property is null</exception>
    public async Task<List<string>> CheckRulesAsync(IPropertyInfo property, TimeSpan timeout)
    {
      var affectedProperties = CheckRules(property);
      await WaitForAsyncRulesToComplete(timeout);
      return affectedProperties;
    }

    /// <summary>
    /// Invokes all rules for the business type.
    /// </summary>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property. Does not return until all async rules are complete.
    /// </returns>
    public Task<List<string>> CheckRulesAsync()
    {
      return CheckRulesAsync(int.MaxValue);
    }

    /// <summary>
    /// Invokes all rules for the business type.
    /// </summary>
    /// <param name="timeout">Timeout value in milliseconds</param>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property. Does not return until all async rules are complete.
    /// </returns>
    public Task<List<string>> CheckRulesAsync(int timeout)
    {
      return CheckRulesAsync(TimeSpan.FromMilliseconds(timeout));
    }

    /// <summary>
    /// Invokes all rules for the business type.
    /// </summary>
    /// <param name="timeout">Timeout value.</param>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property. Does not return until all async rules are complete.
    /// </returns>
    public async Task<List<string>> CheckRulesAsync(TimeSpan timeout)
    {
      var result = CheckRules();
      await WaitForAsyncRulesToComplete(timeout);
      return result;
    }

    private async Task WaitForAsyncRulesToComplete(TimeSpan timeout)
    {
      if (!RunningAsyncRules)
      {
        return;
      }

      var tasks = new Task[] { BusyChanged.WaitAsync(), Task.Delay(timeout) };
      var final = await Task.WhenAny(tasks);
      if (final == tasks[1])
        throw new TimeoutException(nameof(CheckRulesAsync));
    }

    /// <summary>
    /// Invokes all rules for the business type.
    /// </summary>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property.
    /// </returns>
    public List<string> CheckRules()
    {
      if (_suppressRuleChecking)
        return new List<string>();

      RunningRules = true;
      var affectedProperties = CheckObjectRules(RuleContextModes.CheckRules, false);
      var properties = TypeRules.Rules.Where(p => p.PrimaryProperty != null)
                                          .Select(p => p.PrimaryProperty)
                                          .Distinct();
      foreach (var property in properties)
        affectedProperties.AddRange(CheckRules(property, RuleContextModes.CheckRules));
      RunningRules = false;
      if (!RunningRules && !RunningAsyncRules)
        _target.AllRulesComplete();
      return affectedProperties.Distinct().ToList();
    }

    /// <summary>
    /// Invokes all rules attached at the class level
    /// of the business type.
    /// </summary>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property.
    /// </returns>
    public List<string> CheckObjectRules()
    {
      return CheckObjectRules(RuleContextModes.CheckObjectRules, true);
    }

    /// <summary>
    /// Invokes all rules attached at the class level
    /// of the business type.
    /// </summary>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property.
    /// </returns>
    private List<string> CheckObjectRules(RuleContextModes executionContext, bool cascade)
    {
      if (_suppressRuleChecking)
        return new List<string>();

      var oldRR = RunningRules;
      RunningRules = true;
      var rules = from r in TypeRules.Rules
                  where r.PrimaryProperty == null
                    && CanRunRule(_applicationContext, r, executionContext)
                  orderby r.Priority
                  select r;
      BrokenRules.ClearRules(null);
      // Changed to cascade propertyrule to make async ObjectLevel rules rerun PropertLevel rules.
      var firstResult = RunRules(rules, false, executionContext);

      // rerun property level rules for affected properties 
      if (cascade)
      {
        var propertiesToRun = new List<IPropertyInfo>();
        foreach (var item in rules)
          if (!item.IsAsync)
          {
            foreach (var p in item.AffectedProperties)
              propertiesToRun.Add(p);
          }
        // run rules for affected properties
        foreach (var item in propertiesToRun.Distinct())
        {
          var doCascade = false;
          if (CascadeOnDirtyProperties)
            doCascade = firstResult.DirtyProperties.Any(p => p == item.Name);
          firstResult.AffectedProperties.AddRange(CheckRulesForProperty(item, doCascade,
                                                            executionContext | RuleContextModes.AsAffectedProperty));
        }
      }

      RunningRules = oldRR;
      if (!RunningRules && !RunningAsyncRules)
        _target.AllRulesComplete();
      return firstResult.AffectedProperties.Distinct().ToList();
    }

    /// <summary>
    /// Invokes all rules for a specific property of the business type.
    /// </summary>
    /// <param name="property">Property to check.</param>
    /// <returns>
    /// Returns a list of property names affected by the invoked rules.
    /// The PropertyChanged event should be raised for each affected
    /// property.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">If property is null</exception>
    public List<string> CheckRules(IPropertyInfo property)
    {
      return CheckRules(property, RuleContextModes.PropertyChanged);
    }

    private List<string> CheckRules(Csla.Core.IPropertyInfo property, RuleContextModes executionContext)
    {
      if (property == null)
        throw new ArgumentNullException(nameof(property));

      if (_suppressRuleChecking)
        return new List<string>();

      var oldRR = RunningRules;
      RunningRules = true;

      var affectedProperties = new List<string>();
      affectedProperties.AddRange(CheckRulesForProperty(property, true, executionContext));

      RunningRules = oldRR;
      if (!RunningRules && !RunningAsyncRules)
        _target.AllRulesComplete();
      return affectedProperties.Distinct().ToList();
    }

    /// <summary>
    /// Determines whether this rule can run the specified context mode.
    /// </summary>
    /// <param name="applicationContext"></param>
    /// <param name="rule">The rule.</param>
    /// <param name="contextMode">The context mode.</param>
    /// <returns>
    /// 	<c>true</c> if this instance [can run rule] the specified context mode; otherwise, <c>false</c>.
    /// </returns>
    internal static bool CanRunRule(ApplicationContext applicationContext, IBusinessRuleBase rule, RuleContextModes contextMode)
    {
      // default then just return true
      if (rule.RunMode == RunModes.Default) return true;

      bool canRun = true;

      if ((contextMode & RuleContextModes.AsAffectedProperty) > 0)
        canRun &= (rule.RunMode & RunModes.DenyAsAffectedProperty) == 0;

      if ((rule.RunMode & RunModes.DenyOnServerSidePortal) > 0)
        canRun &= applicationContext.LogicalExecutionLocation != ApplicationContext.LogicalExecutionLocations.Server;

      if ((contextMode & RuleContextModes.CheckRules) > 0)
        canRun &= (rule.RunMode & RunModes.DenyCheckRules) == 0;

      return canRun;
    }

    /// <summary>
    /// Invokes all rules for a specific property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="cascade">if set to <c>true</c> [cascade].</param>
    /// <param name="executionMode">The execute mode.</param>
    private List<string> CheckRulesForProperty(IPropertyInfo property, bool cascade, RuleContextModes executionMode)
    {
      // checking rules for the primary property
      var primaryRules = from r in TypeRules.Rules
                         where ReferenceEquals(r.PrimaryProperty, property)
                           && CanRunRule(_applicationContext, r, executionMode)
                         orderby r.Priority
                         select r;

      BrokenRules.ClearRules(property);
      var primaryResult = RunRules(primaryRules, cascade, executionMode);
      if (CascadeOnDirtyProperties)
        cascade = cascade || primaryResult.DirtyProperties.Any();
      if (cascade)
      {
        // get properties affected by all rules
        var propertiesToRun = new List<IPropertyInfo>();
        foreach (var item in primaryRules)
          if (!item.IsAsync)
          {
            foreach (var p in item.AffectedProperties)
              if (!ReferenceEquals(property, p))
                propertiesToRun.Add(p);
          }

        // gets a list rules of of "affected" properties by adding
        // PrimaryProperty where property is in InputProperties
        var inputRules = from r in TypeRules.Rules
                         where !ReferenceEquals(r.PrimaryProperty, property)
                               && r.PrimaryProperty != null
                               && r.InputProperties != null
                               && r.InputProperties.Contains(property)
                         select r;

        var dirtyProperties = primaryResult.DirtyProperties;
        var inputProperties = from r in inputRules
                              where !r.CascadeIfDirty || dirtyProperties.Contains(r.PrimaryProperty.Name)
                              select r.PrimaryProperty;

        foreach (var p in inputProperties)
        {
          if (!ReferenceEquals(property, p))
            propertiesToRun.Add(p);
        }
        // run rules for affected properties
        foreach (var item in propertiesToRun.Distinct())
        {
          var doCascade = false;
          if (CascadeOnDirtyProperties)
            doCascade = primaryResult.DirtyProperties.Any(p => p == item.Name);
          primaryResult.AffectedProperties.AddRange(CheckRulesForProperty(item, doCascade,
                                                               executionMode | RuleContextModes.AsAffectedProperty));
        }
      }

      // always make sure to add PrimaryProperty
      primaryResult.AffectedProperties.Add(property.Name);
      return primaryResult.AffectedProperties.Distinct().ToList();
    }

    [NonSerialized]
    private List<IPropertyInfo> _busyProperties;

    private bool _cascadeOnDirtyProperties;

    private List<IPropertyInfo> BusyProperties
    {
      get
      {
        if (_busyProperties == null)
          _busyProperties = new List<IPropertyInfo>();
        return _busyProperties;
      }
    }

    /// <summary>
    /// Runs the enumerable list of rules.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="cascade">if set to <c>true</c> cascade.</param>
    /// <param name="executionContext">The execution context.</param>
    private RunRulesResult RunRules(IEnumerable<IBusinessRuleBase> rules, bool cascade, RuleContextModes executionContext)
    {
      var affectedProperties = new List<string>();
      var dirtyProperties = new List<string>();
      bool anyRuleBroken = false;
      foreach (var rule in rules)
      {
        // implicit short-circuiting
        if (anyRuleBroken && rule.Priority > ProcessThroughPriority)
          break;
        bool complete = false;
        // set up context
        var context = new RuleContext(_applicationContext, r =>
        {
          if (r.Rule.IsAsync)
          {
            lock (SyncRoot)
            {
              // update output values
              if (r.OutputPropertyValues != null)
                foreach (var item in r.OutputPropertyValues)
                {
                  // value is changed add to dirtyValues
                  if (((IManageProperties)_target).LoadPropertyMarkDirty(item.Key, item.Value))
                    r.AddDirtyProperty(item.Key);
                }
              // update broken rules list
              BrokenRules.SetBrokenRules(r.Results, r.OriginPropertyName, rule.Priority);

              // run rules on affected properties for this async rule
              var affected = new List<string>();
              if (cascade)
                foreach (var item in r.Rule.AffectedProperties.Distinct())
                  if (!ReferenceEquals(r.Rule.PrimaryProperty, item))
                  {
                    var doCascade = false;
                    if (CascadeOnDirtyProperties && (r.DirtyProperties != null))
                      doCascade = r.DirtyProperties.Any(p => p.Name == item.Name);
                    affected.AddRange(CheckRulesForProperty(item, doCascade, r.ExecuteContext | RuleContextModes.AsAffectedProperty));
                  }

              // mark each property as not busy
              foreach (var item in r.Rule.AffectedProperties)
              {
                BusyProperties.Remove(item);
                RunningAsyncRules = BusyProperties.Count > 0;
                if (!BusyProperties.Contains(item))
                  _target.RuleComplete(item);
              }

              foreach (var property in affected.Distinct())
              {
                // property is not in AffectedProperties (already signalled to UI)
                if (!r.Rule.AffectedProperties.Any(p => p.Name == property))
                  _target.RuleComplete(property);
              }

              if (!RunningRules && !RunningAsyncRules)
                _target.AllRulesComplete();
            }
          }
          else  // Rule is Sync 
          {
            // update output values
            if (r.OutputPropertyValues != null)
              foreach (var item in r.OutputPropertyValues)
              {
                // value is changed add to dirtyValues
                if (((IManageProperties)_target).LoadPropertyMarkDirty(item.Key, item.Value))
                  r.AddDirtyProperty(item.Key);
              }

            // update broken rules list
            if (r.Results != null)
            {
              BrokenRules.SetBrokenRules(r.Results, r.OriginPropertyName, rule.Priority);

              // is any rules here broken with severity Error
              if (r.Results.Any(p => !p.Success && p.Severity == RuleSeverity.Error))
                anyRuleBroken = true;
            }

            complete = true;
          }
        })
        {
          Rule = rule
        };
        if (rule.PrimaryProperty != null)
          context.OriginPropertyName = rule.PrimaryProperty.Name;
        context.ExecuteContext = executionContext;
        if (!rule.IsAsync || rule.ProvideTargetWhenAsync)
          context.Target = _target;

        // get input properties
        if (rule.InputProperties != null)
        {
          var target = (IManageProperties)_target;
          context.InputPropertyValues = new Dictionary<IPropertyInfo, object>();
          foreach (var item in rule.InputProperties)
          {
            // do not add lazy loaded fields that have no field data.
            if ((item.RelationshipType & RelationshipTypes.LazyLoad) == RelationshipTypes.LazyLoad)
            {
              if (target.FieldExists(item))
                context.InputPropertyValues.Add(item, target.ReadProperty(item));
            }
            else
              context.InputPropertyValues.Add(item, target.ReadProperty(item));
          }
        }

        // mark properties busy
        if (rule.IsAsync)
        {
          lock (SyncRoot)
          {
            // mark each property as busy
            foreach (var item in rule.AffectedProperties)
            {
              var alreadyBusy = BusyProperties.Contains(item);
              BusyProperties.Add(item);
              RunningAsyncRules = true;
              if (!alreadyBusy)
                _target.RuleStart(item);
            }
          }
        }

        // execute (or start executing) rule
        try
        {
          if (rule is IBusinessRule syncRule)
            syncRule.Execute(context);
          else if (rule is IBusinessRuleAsync asyncRule)
            RunAsyncRule(asyncRule, context);
          else
            throw new ArgumentOutOfRangeException(rule.GetType().FullName);
        }
        catch (Exception ex)
        {
          context.AddErrorResult($"{rule.RuleName}:{ex.Message}");
          if (rule.IsAsync)
            context.Complete();
        }

        if (!rule.IsAsync)
        {
          // process results
          if (!complete)
            context.Complete();
          // copy affected property names
          affectedProperties.AddRange(rule.AffectedProperties.Select(c => c.Name));
          // copy output property names
          if (context.OutputPropertyValues != null)
            affectedProperties.AddRange(context.OutputPropertyValues.Select(c => c.Key.Name));
          // copy dirty properties 
          if (context.DirtyProperties != null)
            dirtyProperties.AddRange(context.DirtyProperties.Select(c => c.Name));

          if (context.Results != null)
          {
            // explicit short-circuiting
            if (context.Results.Any(r => r.StopProcessing))
              break;
          }
        }
      }
      // return any synchronous results
      return new RunRulesResult(affectedProperties, dirtyProperties);
    }

    private static async void RunAsyncRule(IBusinessRuleAsync asyncRule, IRuleContext context)
    {
      try
      {
        await asyncRule.ExecuteAsync(context);
      }
      finally
      {
        context.Complete();
      }
    }

    #region DataAnnotations

    /// <summary>
    /// Adds validation rules corresponding to property
    /// data annotation attributes.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void AddDataAnnotations()
    {
      Type metadataType;
#if !NETSTANDARD2_0 || NET8_0_OR_GREATER
      // add data annotations from metadata class if specified
      var classAttList = _target.GetType().GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MetadataTypeAttribute), true);
      if (classAttList.Length > 0)
      {
        metadataType = ((System.ComponentModel.DataAnnotations.MetadataTypeAttribute)classAttList[0]).MetadataClassType;
        AddDataAnnotationsFromType(metadataType);
      }
#endif

      // attributes on class
      metadataType = _target.GetType();
      AddDataAnnotationsFromType(metadataType);
    }

    /// <summary>
    /// Adds data annotation validation rules from the given type.
    /// </summary>
    /// <param name="metadataType">Type of the metadata.</param>
    private void AddDataAnnotationsFromType(Type metadataType)
    {
      var attList = metadataType.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute), true);
      foreach (var att in attList)
        AddRule(new CommonRules.DataAnnotation(null, (System.ComponentModel.DataAnnotations.ValidationAttribute)att));

      // attributes on properties
      var propList = metadataType.GetProperties();
      foreach (var prop in propList)
      {
        attList = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute), true);
        foreach (var att in attList)
        {
          var target = (IManageProperties)_target;
          var pi = target.GetManagedProperties().First(c => c.Name == prop.Name);
          AddRule(new CommonRules.DataAnnotation(pi, (System.ComponentModel.DataAnnotations.ValidationAttribute)att));
        }
      }
    }

    #endregion

    #region MobileObject overrides

    /// <summary>
    /// Override this method to insert your field values
    /// into the MobileFormatter serialization stream.
    /// </summary>
    /// <param name="info">
    /// Object containing the data to serialize.
    /// </param>
    /// <param name="mode">
    /// The StateMode indicating why this method was invoked.
    /// </param>
    protected override void OnGetState(SerializationInfo info, StateMode mode)
    {
      info.AddValue("_processThroughPriority", _processThroughPriority);
      info.AddValue("_ruleSet", _ruleSet);
      info.AddValue("_cascadeWhenChanged", _cascadeOnDirtyProperties);
      base.OnGetState(info, mode);
    }

    /// <summary>
    /// Override this method to retrieve your field values
    /// from the MobileFormatter serialization stream.
    /// </summary>
    /// <param name="info">
    /// Object containing the data to serialize.
    /// </param>
    /// <param name="mode">
    /// The StateMode indicating why this method was invoked.
    /// </param>
    protected override void OnSetState(SerializationInfo info, StateMode mode)
    {
      _processThroughPriority = info.GetValue<int>("_processThroughPriority");
      _ruleSet = info.GetValue<string>("_ruleSet");
      _cascadeOnDirtyProperties = info.GetValue<bool>("_cascadeWhenChanged");
      base.OnSetState(info, mode);
    }

    /// <summary>
    /// Override this method to insert your child object
    /// references into the MobileFormatter serialization stream.
    /// </summary>
    /// <param name="info">
    /// Object containing the data to serialize.
    /// </param>
    /// <param name="formatter">
    /// Reference to MobileFormatter instance. Use this to
    /// convert child references to/from reference id values.
    /// </param>
    protected override void OnGetChildren(SerializationInfo info, MobileFormatter formatter)
    {
      if (_brokenRules != null && _brokenRules.Count > 0)
      {
        SerializationInfo brInfo = formatter.SerializeObject(_brokenRules);
        info.AddChild("_brokenRules", brInfo.ReferenceId);
      }

      base.OnGetChildren(info, formatter);
    }

    /// <summary>
    /// Override this method to retrieve your child object
    /// references from the MobileFormatter serialization stream.
    /// </summary>
    /// <param name="info">
    /// Object containing the data to serialize.
    /// </param>
    /// <param name="formatter">
    /// Reference to MobileFormatter instance. Use this to
    /// convert child references to/from reference id values.
    /// </param>
    protected override void OnSetChildren(SerializationInfo info, MobileFormatter formatter)
    {
      if (info.Children.TryGetValue("_brokenRules", out var child))
      {
        int referenceId = child.ReferenceId;
        _brokenRules = (BrokenRulesCollection)formatter.GetObject(referenceId);
      }

      base.OnSetChildren(info, formatter);
    }
    #endregion

    #region Serialization Notification

    void ISerializationNotification.Deserialized()
    {
      OnDeserializedHandler(new System.Runtime.Serialization.StreamingContext());
    }

    [System.Runtime.Serialization.OnDeserialized]
    private void OnDeserializedHandler(System.Runtime.Serialization.StreamingContext context)
    {
      SyncRoot = new object();
    }

    #endregion

    #region Get All Broken Rules (tree)

    /// <summary>
    /// Gets all nodes in tree that have IsValid = false (and all parents) 
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns>BrukenRulesTree list</returns>
    public static BrokenRulesTree GetAllBrokenRules(object root)
    {
      return GetAllBrokenRules(root, true);
    }
    /// <summary>
    /// Gets all nodes in tree that have broken rules.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="errorsOnly">if set to <c>true</c> will only return objects that gave IsValid = false.</param>
    public static BrokenRulesTree GetAllBrokenRules(object root, bool errorsOnly)
    {
      var list = new BrokenRulesTree();
      long counter = 1;
      long childBrokenRuleCount = 0;
      AddNodeToBrukenRules(ref list, ref counter, null, root, errorsOnly, ref childBrokenRuleCount);

      return list;
    }

    private static void AddNodeToBrukenRules(ref BrokenRulesTree list, ref long counter, object parentKey, object obj, bool errorsOnly, ref long childBrokenRuleCount)
    {
      // is this a single editable object 
      if (obj is BusinessBase bbase)
      {
        var nodeKey = counter++;
        var bo = bbase;
        long myChildBrokenRuleCount = bo.BrokenRulesCollection.Count;
        var node = new BrokenRulesNode { Parent = parentKey, Node = nodeKey, BrokenRules = bo.BrokenRulesCollection, Object = obj };
        list.Add(node);

        // get managed child properties 
        foreach (var child in ((IManageProperties)bo).GetChildren())
        {
          AddNodeToBrukenRules(ref list, ref counter, nodeKey, child, errorsOnly, ref myChildBrokenRuleCount);
        }

        // remove node if it has no child with broken rules. 
        if (!errorsOnly && myChildBrokenRuleCount == 0)
        {
          list.Remove(node);
        }
        if (errorsOnly && bo.IsValid)
        {
          list.Remove(node);
        }
        childBrokenRuleCount += myChildBrokenRuleCount;
      }

      // or a list of EditableObject (both BindingList and ObservableCollection)
      else if (obj is IEditableCollection collection)
      {
        var nodeKey = counter++;
        var isValid = collection.IsValid;
        var node = new BrokenRulesNode { Parent = parentKey, Node = nodeKey, Object = collection, BrokenRules = new BrokenRulesCollection(true) };
        long myChildBrokenRuleCount = 0;

        list.Add(node);

        foreach (var child in (IEnumerable)collection)
        {
          AddNodeToBrukenRules(ref list, ref counter, nodeKey, child, errorsOnly, ref myChildBrokenRuleCount);
        }

        // remove node if it has no child with broken rules. 
        if (!errorsOnly && myChildBrokenRuleCount == 0)
        {
          list.Remove(node);
        }
        if (errorsOnly && isValid)
        {
          list.Remove(node);
        }
        childBrokenRuleCount += myChildBrokenRuleCount;
      }
      return;
    }

    #endregion


    internal class RunRulesResult
    {
      public RunRulesResult(List<string> affectedProperties, List<string> dirtyProperties)
      {
        AffectedProperties = affectedProperties;
        DirtyProperties = dirtyProperties;
      }

      public List<string> AffectedProperties { get; set; }
      public List<string> DirtyProperties { get; set; }
    }

    object IBusinessRules.Target
    {
      get { return Target; }
    }
  }
}