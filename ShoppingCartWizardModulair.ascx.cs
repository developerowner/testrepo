using System;
using System.Web.UI;
using CMS.Web.Helpers;
using Gigantisch.Repository.DataGigantisch;
using CMS.Shopping.Wizard;
using CMS.Shopping;
using CMS.Controls;
using CMS.Controls.Shopping;
using CMS.Shopping.BL;
using CMS.Data.Objects;
using Gigantisch.DataGigantisch.Models.Enums;
using CMS.Shopping.Data;
using Gigantisch.Deurengigant.Wizard;
using Gigantisch.Deurengigant.Shopping.ConfigurationTypes;
using CMS.Data.ObjectsCached;
using WebShop.Modules.Deurengigant;
using Gigantisch.Deurengigant.BL;
using System.Linq;
using System.Linq.Dynamic;
using Gigantisch.Deurengigant.Shopping;

namespace CMS.DeurenGigant.Modules.Shopping
{
    public partial class ShoppingCartWizardModulair : ConfiguratorControl
    {
        protected void SaveAddress_override(object sender, EventArgs e)
        {
            //Om te zorgen dat de persoonsdata opgeslagen word na <Enter>
            //Opmerking duitsland...
            CartCustomerData.save();
        }

        protected void BackToWizard(object sender, EventArgs e)
        {
            ShoppingCartItem item = sender as ShoppingCartItem;

            if (item.WizardData != null)
            {
                var oldconfig = DoorConfiguration.ReadSerializedWizard(item.WizardData);
                if (oldconfig != null)
                {
                    if (oldconfig is CandoInsideDoorConfiguration)
                    {
                        var db = GigantischCore.GetDataContext();
                        var backoffice = Gigantisch.Repository.DataGigantisch.Site.GetByBackOfficeCode(SessionUtility.BackofficeCode, db);
                        //((CandoInsideDoorConfiguration)oldconfig).convertold(backoffice.SiteId, db);
                    }

                    ConfigurationHelper<Configuration>.Configuration = oldconfig;
                    Context.Redirect(SessionUtility.Base + item.WizardUrl + "?returnstep=" + item.WizardReturnStep);
                }
            }
            else
            {
                if (item != null)
                {
                    Session["ProductMainId"] = item.ProductId.ToString();
                    Session["UpdateGroupID"] = item.GroupId;
                    Session["GroupID"] = item.GroupId;
                    Context.Redirect(item.WizardUrl);
                }
            }

        }

        private void SetAssemblyParameters()
        {
            var db = GigantischCore.GetDataContext();

            var doorsInCartIds = ShoppingCartHelper.CustomerShoppingCart.Items.Where(item => item.ProductType == 1).Select(item => item.ProductId).Distinct();
            var doors = db.Doors.Where(d => doorsInCartIds.Contains(d.Id));
            var gardenDoors = doors.Where(d => d.DoorProductLine.DoorCategory.Code == "TUIN");
            var beemsterDoors = doors.Where(d => d.SupplierId == 79);

            bool hasGardenOrBeemsterLabelDoor = gardenDoors.Any() || beemsterDoors.Any();
            bool hasOtherDoorsToo = doors.Count() > gardenDoors.Count() + beemsterDoors.Count();
            bool hasBeemsterDoors = beemsterDoors.Any();
            bool hasGardenDoors = gardenDoors.Any();
            bool canAssemble = hasOtherDoorsToo;
            bool isMinimumAmount = (ShoppingCartHelper.CustomerShoppingCart.Total.SellPrice<=550);

            // Checking if the selected doors in the cart is not TuinDeur and the total amount of order is < 550 Euros then I am disableling assembly checkbox
            if (canAssemble && isMinimumAmount)
            {
                canAssemble = false;
            }

            SetAssemblyEnabled(canAssemble);
            SetAssemblyWarning(hasBeemsterDoors, hasGardenDoors, isMinimumAmount);
        }

        private void SetAssemblyEnabled(bool value)
        {
            CartAssemblyAndMeasurement.EnableAssembly(value);
        }

        private void SetAssemblyWarning(bool beemsterwarning, bool gardendoorwarning, bool isMinimumAmount)
        {
            string warning = "";
            if (beemsterwarning)
                warning = "<div class=\"warning\">De montageservice is niet van toepassing op de barndeur(en).</div>";
            if(gardendoorwarning)
                warning = "<div class=\"warning\">De montageservice is niet van toepassing op de tuindeur(en).</div>";
            if(beemsterwarning && gardendoorwarning)
                warning = "<div class=\"warning\">De montageservice is niet van toepassing op de barn en tuindeur(en).</div>";
            if (!gardendoorwarning && isMinimumAmount)
                warning = "<div class=\"warning\">De montageservice is niet van toepassing op orders onder de € 550,-.</div>";
            if (gardendoorwarning && isMinimumAmount)
                warning = "<div class=\"warning\">De montageservice is niet van toepassing op tuindeuren en orders onder de € 550,-.</div>";

            CartAssemblyAndMeasurement.SetAssemblyWarning(warning);
        }

        protected void SetTransporter()
        {
            var transporter = Transporter.GetByCode("STW", SessionUtility.BackofficeCode);
            //Doen we niet meer voorlopig
            if (ShoppingCartHelper.CustomerShoppingCart.CustomerPickup == true)
            {
                transporter = Transporter.GetByCode("PICKUP", SessionUtility.BackofficeCode);
            }
            ShoppingCartHelper.CustomerShoppingCart.TransporterId = transporter?.Id;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            divErrorMessage.InnerHtml = "";
            if (Page.IsPostBack) return;


            //Eenmalig, aangezien de "zelf ophalen" optie niet meer gebruikt word.
            SetTransporter();

            var db = GigantischCore.GetDataContext();
            
            if (RadWindow1.VisibleOnPageLoad)
                RadWindow1.VisibleOnPageLoad = false;

            if (Page.Request["key"] != null)
            {
                string key = Page.Request.QueryString["key"];
                if (!string.IsNullOrEmpty(key))
                {
                    var cart = Gigantisch.Repository.DataGigantisch.ShoppingCart.GetByEMailLinkKey(key);
                    if (cart != null)
                    {
                        ShoppingCartHelper.LoadFromDb(cart);
                        DeurenShoppingCartHelper.RebuildPackageActionItems();

                        ShoppingCartHelper.CustomerShoppingCart.PaymentTypeId = (int)OrderPaymentTypeID.FULL;
                        PaymentTypeHelper.RecalculatePrePayment(db);

                        //Recalculate rembours
                        var cmsdb = CmsData.GetDataContext();
                        var backoffice = Gigantisch.Repository.DataGigantisch.Site.GetByBackOfficeCode(SessionUtility.BackofficeCode, db);
                        CMS.Shopping.Data.PrePaymentCost.RecalculateCalculatePrePaymentCost(backoffice.SiteId, db);
                        CMS.Shopping.Data.PaymentDiscount.RecalculateDiscountOnPaymentCost(backoffice.SiteId, db, cmsdb);

                        UpdateIndexPage();
                    }
                }
            }

            var products = ProductType.GetProductsOnly(db).Select(it => it.Id);
            var hasItems = ShoppingCartHelper.CustomerShoppingCart.Items.Any(item => products.Contains(item.ProductType));
            divServiceOptions.Visible = hasItems;

            CheckOudeServicePossibility();
            CheckPaymentPossibility();
            SetAssemblyParameters();

            var do_afronden = Request.RawUrl.ToString().ToLower().Contains("bedankt");
            if (do_afronden)
            {
                MultiView1.SetActiveView(bedankt);
                bool cancel = false;
                KGShoppingCartStep41.load(ref cancel, false);
            }
            else
            {
                bool cancel = false;
                CartMainItems.load(ref cancel, false);

                if (MultiView1.ActiveViewIndex > 0)
                    SetVirtualUrl("/winkelwagen/" + MultiView1.GetActiveView().ID);
            }

            MultiViewButtons1.SaveVisible = Page.User.IsInRole("Administrator") || Page.User.IsInRole("Sales");
            MultiViewButtons1.SetButtons();
        }

        protected void FaultDetected(object sender, WizardErrorEventArgs e)
        {
            MultiView1.ActiveViewIndex = e.ReturnStep;
            divErrorMessage.InnerHtml = e.ErrorMessage;
        }

        protected void SaveContents(ref bool cancel, ref int step)
        {
            cancel = !CartMainItems.Save();
        }

        //protected void SaveContact(ref bool cancel)
        //{
        //    cancel = !CartCustomerData.save();
        //}

        protected void LoadContents(ref bool cancel)
        {
            cancel = false;
            CartMainItems.load(ref cancel, false);
        }
        protected void LoadContact(ref bool cancel)
        {
            CartCustomerData.load();
            cancel = false;
        }

        protected void LoadPayment(ref bool cancel)
        {
            CartDiscountCoupon.Recalculate();
            CartRemboursOptions.Recalculate();
            CartPaymentMethods.load();
            CartPaymentTotals.load();
            cancel = false;
        }

        protected void LoadResult(ref bool cancel)
        {
            cancel = false;
            KGShoppingCartStep41.load(ref cancel, false);
        }

        protected void btnStartPayment_Click(object sender, EventArgs e)
        {
            bool cancel = false;
            StartPayment(ref cancel);
        }

        protected void StartPayment(ref bool cancel)
        {
            if (CartCustomerData.save() == false)
            {
                cancel = true;
                return;
            }

            string clientId = null;
            if (Page is IndexPage indexPage)
            {
                clientId = indexPage.GetClientID();
            }

            //This makes is possible to temporarily disable afterpay without selecting a payment type
            if(ShoppingCartHelper.CustomerShoppingCart.PaymentTypeId == null && CartRemboursOptions.EnableAfterPay == false)
            {
                ShoppingCartHelper.CustomerShoppingCart.PaymentTypeId = (int)OrderPaymentTypeID.FULL;
            }

            var paymenthelper = new CartPaymentHelper();
            if (paymenthelper.StartPayment("/winkelwagen/bedankt", false, clientId: clientId) == false)
            {
                cancel = true;
                divErrorMessage.InnerHtml = string.Join("<br />", paymenthelper.Errors);

                if(ShoppingCartHelper.CustomerShoppingCart.PaymentMethodId == (int)OrderPaymentMethodID.BUCKAROO_AFTERPAY)
                {
                    //Disable the option and set the method to full payment
                    CartRemboursOptions.EnableAfterPay = false;

                    ShoppingCartHelper.CustomerShoppingCart.DisableAfterpay = true;
                    ShoppingCartHelper.CustomerShoppingCart.PaymentMethodId = null;
                    ShoppingCartHelper.CustomerShoppingCart.PaymentTypeId = (int)OrderPaymentTypeID.FULL;

                    var db = GigantischCore.GetDataContext();
                    var backoffice = Gigantisch.Repository.DataGigantisch.Site.GetByBackOfficeCode(SessionUtility.BackofficeCode, db);
                    CMS.Shopping.Data.PrePaymentCost.RecalculateCalculatePrePaymentCost(backoffice.SiteId, db);

                    CartDiscountCoupon.Recalculate();
                    CartRemboursOptions.Recalculate();
                    CartPaymentMethods.load();
                    CartSubtotals.load();
                    CartPaymentTotals.load();
                }

                SetVirtualUrl("/winkelwagen/" + MultiView1.GetActiveView().ID + "?error=" + Server.UrlEncode(divErrorMessage.InnerText));
            }
        }
        protected void PaymentCancelled(object sender, EventArgs e)
        {
            var db = GigantischCore.GetDataContext();
            var products = ProductType.GetProductsOnly(db).Select(it => it.Id);
            var hasItems = ShoppingCartHelper.CustomerShoppingCart.Items.Any(item => products.Contains(item.ProductType));
            divServiceOptions.Visible = hasItems;

            CheckPaymentPossibility();
            UpdateIndexPage();

            SetVirtualUrl("/cancelled.htm");
        }

        protected void PaymentFailed(object sender, EventArgs e)
        {
            SetVirtualUrl("/failed.htm");
        }

        private void SetVirtualUrl(string url)
        {
            ((Index)Page).SetVirtualUrl(url);
        }

        protected void PaymentSucceeded(object sender, EventArgs e)
        {
            MultiViewNavigatorHorizontal1.Disable();
            MultiViewButtons4.Disable();

            //Transaction.AddTransaction(sender as Order, ((Index)Page).Analytics);
            ShoppingCartHelper.CustomerShoppingCart = null;
        }

        protected void NoPaymentPossible(object sender, EventArgs e)
        {
            MultiViewButtons1.NextVisible = false;
            MultiViewButtons1.Custom2Visible = true;
            MultiViewButtons1.SetButtons();
        }

        protected void PaymentPossible(object sender, EventArgs e)
        {
            MultiViewButtons1.NextVisible = true;
            MultiViewButtons1.Custom2Visible = false;
            MultiViewButtons1.SetButtons();
        }

        protected void CartEmpty(object sender, EventArgs e)
        {
            MultiViewButtons1.NextVisible = false;
            MultiViewButtons1.Custom2Visible = false;
            MultiViewButtons1.Custom1Visible = true;
            MultiViewButtons1.SetButtons();
        }

        protected void ItemDeleted(object sender, EventArgs e)
        {
            var cmsdb = CmsData.GetDataContext();
            BLConstruction.Recalculate(cmsdb, SessionUtility.SiteId);
            DoorDeliveryCost.Recalculate(cmsdb, SessionUtility.SiteId, false);
            CartDiscountCoupon.Recalculate();
            
            CartAssemblyAndMeasurement.Recalculate();
            CartRemboursOptions.Recalculate();
            SetAssemblyParameters();
            CheckPaymentPossibility();

            CartSubtotals.load();
            CartPaymentTotals.load();
            UpdateIndexPage();
        }

        protected void GoMeasureRequest(ref bool cancel, ref int step)
        {
            //var effe = this.Context.Request.ApplicationPath.Length == 1;
            //string DomainSSL = this.Context.Request.ApplicationPath.Length == 1 ? SessionUtility.DomainNameSSL : SessionUtility.DomainName;
            string DomainSSL = SessionUtility.Base;
            var db = CMS.Data.Objects.CmsData.GetDataContext();
            var param = ParameterCached.GetByNameAndSiteId("InmeetPage", SessionUtility.SiteId, db);

            Context.Redirect(string.Format("{0}/{1}", DomainSSL, param.Value));
        }

        protected void GoOfferte(ref bool cancel, ref int step)
        {
            //string DomainSSL = this.Context.Request.ApplicationPath.Length == 1 ? SessionUtility.DomainNameSSL : SessionUtility.DomainName;
            string DomainSSL = SessionUtility.Base;
            var db = CMS.Data.Objects.CmsData.GetDataContext();
            var param = ParameterCached.GetByNameAndSiteId("OffertePage", SessionUtility.SiteId, db);

            Context.Redirect(string.Format("{0}/{1}", DomainSSL, param.Value));
        }

        protected void SaveOrder(ref bool cancel, ref int step)
        {
            SaveShoppingCartWrapper1.Show();
        }

        new protected void MultiView1_ActiveViewChanged(object sender, EventArgs e)
        {
            if (!IsPostBack) return;

            bool cancel = false;
            switch (MultiView1.ActiveViewIndex)
            {
                case 0:
                    LoadContents(ref cancel);
                    break;

                case 1:
                    LoadContact(ref cancel);
                    break;

                case 2:
                    LoadPayment(ref cancel);
                    break;

                default:
                    break;
            }

            SetVirtualUrl("/winkelwagen/" + MultiView1.GetActiveView().ID);

            UpdateIndexPage();
        }

        private void UpdateIndexPage()
        {
            if (this.Page is IndexPage page)
            {
                var kgheader = page.LoadedUserControls[nameof(DGHeader)] as DGHeader;
                kgheader?.UpdateCartButton();
            }
        }

        protected void KGShoppingCartStep11_ServiceChanged(object sender, EventArgs e)
        {
            CheckPaymentPossibility();
            CheckOudeServicePossibility();

            CartDiscountCoupon.Recalculate();
            CartRemboursOptions.Recalculate();

            CartSubtotals.load();
            CartPaymentTotals.load();
            UpdateIndexPage();
        }

        protected void KGShoppingCartStep11_QuantityChanged(object sender, EventArgs e)
        {
            var cmsdb = CmsData.GetDataContext();
            var db = GigantischCore.GetDataContext();

            BLConstruction.Recalculate(cmsdb, SessionUtility.SiteId);
            DoorDeliveryCost.Recalculate(cmsdb, SessionUtility.SiteId, false);

            var backoffice = Gigantisch.Repository.DataGigantisch.Site.GetByBackOfficeCode(SessionUtility.BackofficeCode, db);
            CMS.Shopping.Data.PrePaymentCost.RecalculateCalculatePrePaymentCost(backoffice.SiteId, db);
            CMS.Shopping.Data.PaymentDiscount.RecalculateDiscountOnPaymentCost(backoffice.SiteId, db, cmsdb);

            CheckPaymentPossibility();

            CartDiscountCoupon.Recalculate();
            CartAssemblyAndMeasurement.Recalculate();
            CartRemboursOptions.Recalculate();

            CartSubtotals.load();
            CartPaymentTotals.load();
            SetAssemblyParameters();
            UpdateIndexPage();
        }

        protected void CartRemboursOptions_CartRemboursChanged(object sender, EventArgs e)
        {
            CheckPaymentPossibility();

            CartCustomerData.save();

            CartSubtotals.load();
            CartPaymentTotals.load();

            UpdateIndexPage();
            CartPaymentMethods.load();
            CartCustomerData.load();
        }

        private void CheckPaymentPossibility()
        {
            bool setafspraak = ShoppingCartHelper.CustomerShoppingCart.MeasurementService;
            if (setafspraak)
            {
                NoPaymentPossible(this, new EventArgs());
            }
            else
            {
                PaymentPossible(this, new EventArgs());
            }
        }

        private void CheckOudeServicePossibility()
        {
            //Disable oudedeurenservice als montage actief staat

            var hasmontaqge = ShoppingCartHelper.CustomerShoppingCart.AssemblyService;
            CartOldDoorService.Visible = !hasmontaqge;
            if(hasmontaqge)
            {
                CartOldDoorService.SetOudeDeuren(false, 1);
            } else
            {
                CartOldDoorService.load();
            }
        }

        protected void CartOldDoorServiceChanged(object sender, EventArgs e)
        {
            CheckPaymentPossibility();
            CartDiscountCoupon.Recalculate();
            CartRemboursOptions.Recalculate();

            CartSubtotals.load();
            CartPaymentTotals.load();
            UpdateIndexPage();
        }

        protected void CartDiscountCoupon_CouponUpdated(object sender, EventArgs e)
        {
            CartCustomerData.save();

            var db = GigantischCore.GetDataContext();
            var backoffice = Gigantisch.Repository.DataGigantisch.Site.GetByBackOfficeCode(SessionUtility.BackofficeCode, db);
            CMS.Shopping.Data.PrePaymentCost.RecalculateCalculatePrePaymentCost(backoffice.SiteId, db);

            CartPaymentMethods.load();
            CartPaymentTotals.Update();
            CartRemboursOptions.Recalculate();

            UpdateIndexPage();
        }
    }
}