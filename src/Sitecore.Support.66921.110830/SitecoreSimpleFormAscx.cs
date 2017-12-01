using Sitecore.Diagnostics;
using Sitecore.Form.Core.Ascx.Controls;
using Sitecore.Form.Core.Attributes;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Utility;
using System;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Sitecore.Form.Web.UI.Controls
{
  [Dummy, System.Web.UI.PersistChildren(true), System.Web.UI.ToolboxData("<div runat=\"server\"></div>")]
  public class SitecoreSimpleFormAscx : SitecoreSimpleForm
  {
    protected override FormTitle Title
    {
      get
      {
        return this.title;
      }
    }

    protected override FormIntroduction Intro
    {
      get
      {
        return this.intro;
      }
    }

    protected override FormFooter Footer
    {
      get
      {
        return this.footer;
      }
    }

    protected override FormSubmit Submit
    {
      get
      {
        return this.submit;
      }
    }

    protected override SubmitSummary SubmitSummary
    {
      get
      {
        return this.submitSummary;
      }
    }

    [Obsolete("Use SubmitSummary")]
    protected override System.Web.UI.WebControls.Label Error
    {
      get
      {
        return null;
      }
    }

    protected override System.Web.UI.Control FieldContainer
    {
      get
      {
        return this.fieldContainer;
      }
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
      this.title.Item = base.FormItem.InnerItem;
      this.title.SetTagKey(base.FormItem.TitleTag);
      this.title.DisableWebEditing = base.DisableWebEditing;
      this.title.Parameters = base.Parameters;
      this.title.FastPreview = base.FastPreview;
      this.intro.Item = base.FormItem.InnerItem;
      this.intro.DisableWebEditing = base.DisableWebEditing;
      this.intro.Parameters = base.Parameters;
      this.intro.FastPreview = base.FastPreview;
      this.submit.Item = base.FormItem.InnerItem;
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
      this.footer.Item = base.FormItem.InnerItem;
      this.footer.DisableWebEditing = base.DisableWebEditing;
      this.footer.Parameters = base.Parameters;
      this.footer.FastPreview = base.FastPreview;
      this.EventCounter.ID = this.ID + SimpleForm.prefixEventCountID;
      this.Controls.Add(this.EventCounter);
      this.AntiCsrf.ID = this.ID + SimpleForm.PrefixAntiCsrfId;
      this.Controls.Add(this.AntiCsrf);
      object obj = SessionUtil.GetSessionValue<object>(this.AntiCsrf.ID);
      if (obj == null)
      {
        obj = Guid.NewGuid().ToString();
        SessionUtil.SetSessionValue(this.AntiCsrf.ID, obj);
      }
      if (base.IsPostBack && base.Request.Form.AllKeys.Any((string k) => k != null && k.Contains(this.submit.ID)))
      {
        return;
      }
      this.AntiCsrf.Value = obj.ToString();
    }
  }
}