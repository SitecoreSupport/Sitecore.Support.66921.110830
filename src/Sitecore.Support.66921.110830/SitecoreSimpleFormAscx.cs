using Sitecore.Diagnostics;
using Sitecore.Form.Core;
using Sitecore.Form.Core.Ascx.Controls;
using Sitecore.Form.Core.Client.Submit;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Data;
using Sitecore.Form.Core.Pipelines.FormSubmit;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Forms.Core.Data;
using Sitecore.Pipelines;
using Sitecore.WFFM.Abstractions;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Helpers;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Sitecore.Support.Form.Web.UI.Controls
{
  public class SitecoreSimpleFormAscx : Sitecore.Form.Web.UI.Controls.SitecoreSimpleFormAscx
  {
    private readonly IActionExecutor actionExecutor;

    private readonly IAnalyticsTracker analyticsTracker;

    private new EventHandler<EventArgs> FailedSubmit;

    private new EventHandler<EventArgs> SucceedSubmit;

    private new EventHandler<EventArgs> SucceedValidation;

    public SitecoreSimpleFormAscx()
    {
      this.EventCounter = new HiddenField();
      this.AntiCsrf = new HiddenField();
      this.actionExecutor = DependenciesManager.ActionExecutor;
      this.analyticsTracker = DependenciesManager.AnalyticsTracker;
    }

    protected override void OnInit(EventArgs e)
    {
      Sitecore.Diagnostics.Assert.IsNotNull(base.FormItem, "FormItem");
      if (this.Page == null)
      {
        this.Page = WebUtil.GetPage();
        ReflectionUtils.SetField(typeof(System.Web.UI.Page), this.Page, "_enableEventValidation", false);
      }
      this.Page.EnableViewState = true;
      ThemesManager.RegisterCssScript(this.Page, base.FormItem.InnerItem, Sitecore.Context.Item);
      typeof(FormTitle).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public).SetValue(this.title, base.FormItem.InnerItem);
      this.title.SetTagKey(base.FormItem.TitleTag);
      this.title.DisableWebEditing = base.DisableWebEditing;
      this.title.Parameters = base.Parameters;
      this.title.FastPreview = base.FastPreview;
      typeof(FormIntroduction).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public).SetValue(this.intro, base.FormItem.InnerItem);
      this.intro.DisableWebEditing = base.DisableWebEditing;
      this.intro.Parameters = base.Parameters;
      this.intro.FastPreview = base.FastPreview;
      typeof(FormSubmit).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public).SetValue(this.submit, base.FormItem.InnerItem);
      this.submit.ID = this.ID + SitecoreSimpleForm.PrefixSubmitID;
      this.submit.DisableWebEditing = base.DisableWebEditing;
      this.submit.Parameters = base.Parameters;
      this.submit.FastPreview = base.FastPreview;
      this.submit.ValidationGroup = this.submit.ID;
      this.submit.Click += new EventHandler(this.OnClick);
      if (base.FastPreview)
      {
        this.summary.Visible = false;
      }
      this.summary.ID = SimpleForm.prefixSummaryID;
      this.summary.ValidationGroup = this.submit.ID;
      this.submitSummary.ID = this.ID + SimpleForm.prefixErrorID;
      base.Expand();
      typeof(FormFooter).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public).SetValue(this.footer, base.FormItem.InnerItem);
      this.footer.DisableWebEditing = base.DisableWebEditing;
      this.footer.Parameters = base.Parameters;
      this.footer.FastPreview = base.FastPreview;
      this.EventCounter.ID = this.ID + SimpleForm.prefixEventCountID;
      this.Controls.Add(this.EventCounter);
      this.AntiCsrf.ID = this.ID + SimpleForm.PrefixAntiCsrfId;
      this.Controls.Add(this.AntiCsrf);

      var oldCookieToken = CookieUtil.GetCookieValue(this.AntiCsrf.ID);
      string cookieToken, formToken;
      AntiForgery.GetTokens(oldCookieToken, out cookieToken, out formToken);
      if (string.IsNullOrEmpty(oldCookieToken))
      {
        CookieUtil.SetCookie(this.AntiCsrf.ID, cookieToken);
      }

      if (base.IsPostBack && base.Request.Form.AllKeys.Any((string k) => k != null && k.Contains(this.submit.ID)))
      {
        return;
      }

      this.AntiCsrf.Value = formToken;
    }

    private bool ValidateAntiCsrf()
    {
      try
      {
        var cookieToken = CookieUtil.GetCookieValue(this.AntiCsrf.ID);
        var formToken = this.AntiCsrf.Value;

        // if it's invalid, will throw an exception
        AntiForgery.Validate(cookieToken, formToken);

        return true;
      }
      catch { return false; }
    }

    protected override void OnClick(object sender, EventArgs e)
    {
      Assert.ArgumentNotNull(sender, "sender");

      if (!ValidateAntiCsrf())
      {
        var args = new SubmittedFormFailuresArgs(
          this.FormID,
          new[] { new ExecuteResult.Failure { ErrorMessage = "WFFM: Forged request detected!" } })
        { Database = StaticSettings.ContextDatabase.Name };

        CorePipeline.Run("errorSubmit", args);

        Log.Error("WFFM: Forged request detected!", this);
        this.OnRefreshError(args.Failures.Select(f => f.ErrorMessage).ToArray());
        return;
      }

      this.UpdateSubmitAnalytics();
      this.UpdateSubmitCounter();


      bool onSucceedValidation = false;
      System.Web.UI.ValidatorCollection validators = (this.Page ?? new Page()).GetValidators(((Control)sender).ID);
      if (validators.FirstOrDefault(v => !v.IsValid && v is IAttackProtection) != null)
      {
        validators.ForEach(
          v =>
          {
            if (!v.IsValid && !(v is IAttackProtection))
            {
              v.IsValid = true;
            }
          });
      }

      if (this.Page != null && this.Page.IsValid)
      {
        this.RequiredMarkerProccess(this, true);

        var actions = new List<IActionDefinition>();
        this.CollectActions(this, actions);

        try
        {
          FormDataHandler.ProcessData(this.FormID, base.GetChildState().ToArray(), actions.ToArray(), this.actionExecutor);

          this.OnSuccessSubmit();

          OnSucceedValidation(new EventArgs());

          this.OnSucceedSubmit(new EventArgs());
        }
        catch (ThreadAbortException)
        {
          onSucceedValidation = true;
        }
        catch (ValidatorException ex)
        {
          this.OnRefreshError(new string[] { ex.Message });
        }
        catch (FormSubmitException ex)
        {
          onSucceedValidation = true;
          this.OnRefreshError(ex.Failures.Select(f => f.ErrorMessage).ToArray());
        }
        catch (Exception ex)
        {
          try
          {
            var args = new SubmittedFormFailuresArgs(
              this.FormID,
              new[] { new ExecuteResult.Failure { ErrorMessage = ex.Message, StackTrace = ex.StackTrace } })
            { Database = StaticSettings.ContextDatabase.Name };

            CorePipeline.Run("errorSubmit", args);
            this.OnRefreshError(args.Failures.Select(f => f.ErrorMessage).ToArray());
          }
          catch (Exception unknown)
          {
            Log.Error(unknown.Message, unknown, this);
          }

          onSucceedValidation = true;
        }
      }
      else
      {
        this.SetFocusOnError();

        this.TrackValdationEvents(sender, e);

        this.RequiredMarkerProccess(this, false);
      }

      this.EventCounter.Value = (analyticsTracker.EventCounter + 1).ToString();

      if (onSucceedValidation)
      {
        OnSucceedValidation(new EventArgs());
      }

      //TODO: why it is called here????
      this.OnFailedSubmit(new EventArgs());
    }

    private void OnFailedSubmit(EventArgs e)
    {
      EventHandler<EventArgs> failedSubmit = this.FailedSubmit;
      if (failedSubmit != null)
      {
        failedSubmit(this, e);
      }
    }

    private void OnSucceedSubmit(EventArgs e)
    {
      EventHandler<EventArgs> succeedSubmit = this.SucceedSubmit;
      if (succeedSubmit != null)
      {
        succeedSubmit(this, e);
      }
    }

    private void OnSucceedValidation(EventArgs args)
    {
      EventHandler<EventArgs> succeedValidation = this.SucceedValidation;
      if (succeedValidation != null)
      {
        succeedValidation(this, args);
      }
    }

    private void SetFocusOnError()
    {
      if (this.Page != null)
      {
        BaseValidator baseValidator = (BaseValidator)this.Page.Validators.FirstOrDefault((IValidator v) => v is BaseValidator && ((BaseValidator)v).IsFailedAndRequireFocus());
        if (baseValidator != null)
        {
          if (!string.IsNullOrEmpty(baseValidator.Text))
          {
            Control controlToValidate = baseValidator.GetControlToValidate();
            if (controlToValidate != null)
            {
              base.SetFocus(baseValidator.ClientID, controlToValidate.ClientID);
              return;
            }
          }
          else
          {
            Control control = this.FindControl(this.BaseID + SimpleForm.prefixErrorID);
            if (control != null)
            {
              base.SetFocus(control.ClientID, null);
            }
          }
        }
      }
    }

    private void TrackValdationEvents(object sender, EventArgs e)
    {
      if (base.IsDropoutTrackingEnabled)
      {
        this.OnTrackValidationEvent(sender, e);
      }
    }

    private void UpdateSubmitAnalytics()
    {
      if (base.IsAnalyticsEnabled && !base.FastPreview)
      {
        this.analyticsTracker.BasePageTime = base.RenderedTime;
        this.analyticsTracker.TriggerEvent(Sitecore.WFFM.Abstractions.Analytics.IDs.FormSubmitEventId, "Form Submit", this.FormID, string.Empty, this.FormID.ToString());
      }
    }

    private void UpdateSubmitCounter()
    {
      if (base.RobotDetection.Session.Enabled)
      {
        SubmitCounter.Session.AddSubmit(this.FormID, base.RobotDetection.Session.MinutesInterval);
      }
      if (base.RobotDetection.Server.Enabled)
      {
        SubmitCounter.Server.AddSubmit(this.FormID, base.RobotDetection.Server.MinutesInterval);
      }
    }

    private void OnRefreshErrorBase(string message)
    {
      this.OnRefreshError(new string[]
      {
                message
      });
    }

    protected override void OnLoad(EventArgs e)
    {
      this.OnRefreshErrorBase(string.Empty);
      if (!this.Page.IsPostBack && !this.Page.IsCallback && !base.IsTresholdRedirect)
      {
        base.RenderedTime = DateTime.UtcNow;
      }
      this.Page.ClientScript.RegisterClientScriptInclude("jquery", "/sitecore%20modules/web/web%20forms%20for%20marketers/scripts/jquery.js");
      this.Page.ClientScript.RegisterClientScriptInclude("jquery-ui.min", "/sitecore%20modules/web/web%20forms%20for%20marketers/scripts/jquery-ui.min.js");
      this.Page.ClientScript.RegisterClientScriptInclude("jquery-ui-i18n", "/sitecore%20modules/web/web%20forms%20for%20marketers/scripts/jquery-ui-i18n.js");
      this.Page.ClientScript.RegisterClientScriptInclude("json2.min", "/sitecore%20modules/web/web%20forms%20for%20marketers/scripts/json2.min.js");
      this.Page.ClientScript.RegisterClientScriptInclude("head.load.min", "/sitecore%20modules/web/web%20forms%20for%20marketers/scripts/head.load.min.js");
      this.Page.ClientScript.RegisterClientScriptInclude("sc.webform", "/sitecore%20modules/web/web%20forms%20for%20marketers/scripts/sc.webform.js?v=17072012");
      if (base.IsAnalyticsEnabled && !base.FastPreview)
      {
        IAnalyticsTracker analyticsTracker = (IAnalyticsTracker)this.GetType().BaseType.BaseType.BaseType.BaseType.GetField("analyticsTracker", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).GetValue(this);
        analyticsTracker.GetType().GetProperty("BasePageTime", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).SetValue(analyticsTracker, base.RenderedTime);
        base.EventCounter.Value = ((int)analyticsTracker.GetType().GetProperty("EventCounter", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).GetValue(analyticsTracker) + 1).ToString();
      }
      this.OnAddInitOnClient();
      this.Page.PreRenderComplete += new EventHandler(base.OnPreRenderComplete);
    }
  }
}