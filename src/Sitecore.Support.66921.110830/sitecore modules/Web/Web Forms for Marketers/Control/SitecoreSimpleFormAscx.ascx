<%@ Control Language="C#" AutoEventWireup="true" Inherits="Sitecore.Support.Form.Web.UI.Controls.SitecoreSimpleFormAscx,Sitecore.Support.66921.110830" %>
<%@ Register Namespace="Sitecore.Form.Web.UI.Controls" Assembly="Sitecore.Forms.Core" TagPrefix="wfm" %>

<wfm:FormTitle ID="title" runat="server"/>
<wfm:FormIntroduction ID="intro" runat="server"/>
<asp:ValidationSummary ID="summary" runat="server" ValidationGroup="submit" CssClass="scfValidationSummary"/>
<wfm:SubmitSummary ID="submitSummary" runat="server" CssClass="scfSubmitSummary"/>
<asp:Panel ID="fieldContainer" runat="server"/>
<wfm:FormFooter ID="footer" runat="server"/>
<wfm:FormSubmit ID="submit" runat="server" Class="scfSubmitButtonBorder"/>
 
