﻿// -------------------------------------------------------------------------------------------
// <copyright file="TemplatePresenter.cs" company="MapWindow OSS Team - www.mapwindow.org">
//  MapWindow OSS Team - 2015
// </copyright>
// -------------------------------------------------------------------------------------------

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MW5.Api.Concrete;
using MW5.Api.Helpers;
using MW5.Plugins.Concrete;
using MW5.Plugins.Helpers;
using MW5.Plugins.Interfaces;
using MW5.Plugins.Mvp;
using MW5.Plugins.Printing.Helpers;
using MW5.Plugins.Printing.Model;
using MW5.Plugins.Printing.Views.Abstract;
using MW5.Plugins.Services;

namespace MW5.Plugins.Printing.Views
{
    internal class TemplatePresenter : BasePresenter<ITemplateView, TemplateModel>
    {
        private const int MaxSizeInches = 75; // maximum size of layout (either width or height) in inches
        private readonly IAppContext _context;

        public TemplatePresenter(ITemplateView view, IAppContext context)
            : base(view)
        {
            _context = context;

            View.LayoutSizeChanged += () =>
                {
                    Validate();
                    View.UpdateView();
                };

            View.FitToPage += OnFitToPageClicked;
        }

        /// <summary>
        /// Called when [fit to page clicked].
        /// </summary>
        private void OnFitToPageClicked()
        {
            GeoSize geoSize;
            if (_context.Map.GetGeodesicSize(View.MapExtents, out geoSize))
            {
                // TODO: choose depending on selected format
                var size = new SizeF(700, 700);   // 7 by 7 inches
                double scale = LayoutScaleHelper.CalcMapScale(geoSize, size);

                View.PopulateScales(Convert.ToInt32(scale));
            }
        }

        /// <summary>
        /// A handler for the IView.OkButton.Click event. 
        /// If the method returns true, View will be closed and presenter.ReturnValue set to true.
        /// If the method return false, no actions are taken, so View.Close, presenter.ReturnValue
        /// should be called / set manually.
        /// </summary>
        public override bool ViewOkClicked()
        {
            if (!Validate())
            {
                string msg = View.IsNewLayout ? "Invalid layout size." : "No template is selected.";
                MessageService.Current.Info(msg);
                return false;
            }

            Model.PaperFormat = View.PaperFormat;
            Model.PaperOrientation = View.Orientation;
            Model.TemplateName = TemplateFilename;
            Model.Extents = View.MapExtents;
            Model.Scale = View.MapScale;

            SaveConfig();

            return true;
        }

        private string TemplateFilename
        {
            get
            {
                if (View.IsNewLayout)
                {
                    return string.Empty;
                }

                var template = View.Template;
                return template != null ? template.Filename : string.Empty;
            }
        }

        /// <summary>
        /// Calculates canvas size in pixels.
        /// </summary>
        private bool CalculateCanvasSize(out SizeF size)
        {
            size = default(SizeF);

            GeoSize geoSize;
            if (!_context.Map.GetGeodesicSize(View.MapExtents, out geoSize))
            {
                return false;
            }

            size = LayoutScaleHelper.CalcMapSize(View.MapScale, geoSize);
            return true;
        }

        /// <summary>
        /// Calculates the number of pages
        /// </summary>
        private void CalculatePageCount(SizeF size)
        {
            var paperSize = PaperSizes.PaperSizeByFormatName(View.PaperFormat, PrinterManager.PrinterSettings);
            if (paperSize != null)
            {
                // TODO: subtract margins
                //float width = paperSize.Width - LayoutControl.DefaultMarginWidth * 2;
                //float height = paperSize.Height - LayoutControl.DefaultMarginHeight * 2;

                bool swap = View.Orientation == Orientation.Vertical;
                float width = swap ? paperSize.Height : paperSize.Width;
                float height = swap ? paperSize.Width : paperSize.Height;

                Model.PageCountX = (int)Math.Ceiling(size.Width / width);
                Model.PageCountY = (int)Math.Ceiling(size.Height / height);
            }
            else
            {
                Model.PageCountX = -1;
                Model.PageCountY = -1;
            }
        }

        private void SaveConfig()
        {
            var config = AppConfig.Instance;

            config.PrintingOrientation = View.Orientation;
            config.PrintingPaperFormat = View.PaperFormat;
            config.PrintingScale = View.MapScale;
            config.PrintingTemplate = TemplateFilename;
        }

        /// <summary>
        /// Calculates canvas size and returns true if it's within limits.
        /// </summary>
        private bool Validate()
        {
            if (View.IsNewLayout)
            {
                SizeF size;
                if (!CalculateCanvasSize(out size))
                {
                    return false;
                }

                Model.Valid = size.Width * size.Height / 10000 <= Math.Pow(MaxSizeInches, 2.0);

                CalculatePageCount(size);

                return Model.Valid;
            }

            return View.Template != null;
        }
    }
}