﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web.Mvc;
using A = DocumentFormat.OpenXml.Drawing;
using A14 = DocumentFormat.OpenXml.Office2010.Drawing;
using Ap = DocumentFormat.OpenXml.ExtendedProperties;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using C14 = DocumentFormat.OpenXml.Office2010.Drawing.Charts;
using C15 = DocumentFormat.OpenXml.Office2013.Drawing.Chart;
using Cs = DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Op = DocumentFormat.OpenXml.CustomProperties;
using Thm15 = DocumentFormat.OpenXml.Office2013.Theme;
using Vt = DocumentFormat.OpenXml.VariantTypes;
using We = DocumentFormat.OpenXml.Office2013.WebExtension;
using Wetp = DocumentFormat.OpenXml.Office2013.WebExtentionPane;
using X14 = DocumentFormat.OpenXml.Office2010.Excel;
using X15 = DocumentFormat.OpenXml.Office2013.Excel;
using X15ac = DocumentFormat.OpenXml.Office2013.ExcelAc;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace Focus.Controllers
{
    public class ReportController : Controller
    {
        private List<string> progress = new List<string>() { "Not started", "In Progress", "Completed" };
        private List<string> rowArry = new List<string>() { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
        private List<string> headers = new List<string>()
                { "Task", "Owner", "Email", "Bucket", "Progress", "Due Date", "Completed Date", "Completed By", "Created Date", "Task Id" };
        private reportDataObject reportData;
        // GET: Report
        public async Task<JsonResult> generateReport()
        {

            var jsonStringData = new StreamReader(this.Request.InputStream).ReadToEnd();


            try
            {

                reportData = JsonSerializer.Deserialize<reportDataObject>(jsonStringData);
            }
            catch (Exception e)
            {
                Console.WriteLine("generateReport: Json Serialize failed: {0}", e.Message);
            }

            coverSmallPartData = reportData.smallPartImageData;
            coverLargePartData = reportData.largePartImageData;

            Stream packageStream = new MemoryStream();
            using (SpreadsheetDocument package = SpreadsheetDocument.Create(packageStream, SpreadsheetDocumentType.Workbook))
            {
                CreateParts(package);
                package.Save();
            }

            packageStream.Flush();
            //reset the position to the start of the stream
            packageStream.Seek(0, SeekOrigin.Begin);

            var graphServiceClient = new GraphServiceClient(new DelegateAuthenticationProvider(async (requestMessage) =>
            {
                requestMessage
                    .Headers
                    .Authorization = new AuthenticationHeaderValue("bearer", reportData.graphToken);

                await Task.FromResult<object>(null);
            }));

            string putUrl = graphServiceClient.Groups.AppendSegmentToRequestUrl(reportData.groupId + "/drive/root:/" + reportData.channelName
                + "/" + reportData.channelName + "Report.xlsx" + ":/content");

            try
            {
                DriveItem uploadedItem = await graphServiceClient.Groups[reportData.groupId].Drive.Root
                    .ItemWithPath(reportData.channelName + "/" + reportData.channelName + "Report.xlsx")
                    .Content.Request().PutAsync<DriveItem>(packageStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("generateReport: Graph OneDrive upload failed: {0}", e.Message);
            }

            //{
            //    "id": "0123456789abc",
            //    "name": "FileB.txt",
            //    "size": 35,
            //    "file": { }
            //}
            JsonResult jres = new JsonResult();
            jres.Data = "{ reportFile: " + reportData.channelName + "Reportl.xlsx }";
            //jres.Data = jsonStringData.ToString();
            return jres;
        }

        private void CreateParts(SpreadsheetDocument document)
        {
            WebExTaskpanesPart webExTaskpanesPart1 = document.AddNewPart<WebExTaskpanesPart>("rId2");
            GenerateWebExTaskpanesPart1Content(webExTaskpanesPart1);

            WebExtensionPart webExtensionPart1 = webExTaskpanesPart1.AddNewPart<WebExtensionPart>("rId1");
            GenerateWebExtensionPart1Content(webExtensionPart1);

            WorkbookPart workbookPart1 = document.AddWorkbookPart();
            GenerateWorkbookPart1Content(workbookPart1);

            SharedStringTablePart sharedStringTablePart1 = workbookPart1.AddNewPart<SharedStringTablePart>("rId8");
            GenerateSharedStringTablePart1Content(sharedStringTablePart1);

            WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>("rId3");
            GenerateWorksheetPart1Content(worksheetPart1);

            //SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart1 = worksheetPart1.AddNewPart<SpreadsheetPrinterSettingsPart>("rId3");
            //GenerateSpreadsheetPrinterSettingsPart1Content(spreadsheetPrinterSettingsPart1);

            PivotTablePart pivotTablePart1 = worksheetPart1.AddNewPart<PivotTablePart>("rId2");
            GeneratePivotTablePart1Content(pivotTablePart1);

            PivotTableCacheDefinitionPart pivotTableCacheDefinitionPart1 = pivotTablePart1.AddNewPart<PivotTableCacheDefinitionPart>("rId1");
            generateCoverPivotTableCacheDefinitionContent(pivotTableCacheDefinitionPart1);
            //GeneratePivotTableCacheDefinitionPart1Content(pivotTableCacheDefinitionPart1);

            PivotTableCacheRecordsPart pivotTableCacheRecordsPart1 = pivotTableCacheDefinitionPart1.AddNewPart<PivotTableCacheRecordsPart>("rId1");
            GeneratePivotTableCacheRecordsPart1Content(pivotTableCacheRecordsPart1);

            PivotTablePart pivotTablePart2 = worksheetPart1.AddNewPart<PivotTablePart>("rId1");
            GeneratePivotTablePart2Content(pivotTablePart2);

            pivotTablePart2.AddPart(pivotTableCacheDefinitionPart1, "rId1");

            DrawingsPart drawingsPart1 = worksheetPart1.AddNewPart<DrawingsPart>("rId4");
            generateCoverDrawingsContent(drawingsPart1);
            //GenerateDrawingsPart1Content(drawingsPart1);

            ImagePart imagePart1 = drawingsPart1.AddNewPart<ImagePart>("image/png", "rId3");
            generateImageCoverSmall(imagePart1);
            //GenerateImagePart1Content(imagePart1);

            ChartPart chartPart1 = drawingsPart1.AddNewPart<ChartPart>("rId2");
            GenerateChartPart1Content(chartPart1);

            ChartColorStylePart chartColorStylePart1 = chartPart1.AddNewPart<ChartColorStylePart>("rId2");
            GenerateChartColorStylePart1Content(chartColorStylePart1);

            ChartStylePart chartStylePart1 = chartPart1.AddNewPart<ChartStylePart>("rId1");
            GenerateChartStylePart1Content(chartStylePart1);

            ChartPart chartPart2 = drawingsPart1.AddNewPart<ChartPart>("rId1");
            GenerateChartPart2Content(chartPart2);

            ChartColorStylePart chartColorStylePart2 = chartPart2.AddNewPart<ChartColorStylePart>("rId2");
            GenerateChartColorStylePart2Content(chartColorStylePart2);

            ChartStylePart chartStylePart2 = chartPart2.AddNewPart<ChartStylePart>("rId1");
            GenerateChartStylePart2Content(chartStylePart2);

            ImagePart imagePart2 = drawingsPart1.AddNewPart<ImagePart>("image/png", "rId4");
            generateImageCoverLarge(imagePart2);
            //GenerateImagePart2Content(imagePart2);

            WorkbookStylesPart workbookStylesPart1 = workbookPart1.AddNewPart<WorkbookStylesPart>("rId7");
            GenerateWorkbookStylesPart1Content(workbookStylesPart1);

            //WorksheetPart worksheetPart2 = workbookPart1.AddNewPart<WorksheetPart>("rId2");
            //GenerateWorksheetPart2Content(worksheetPart2);

            //SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart2 = worksheetPart2.AddNewPart<SpreadsheetPrinterSettingsPart>("rId1");
            //GenerateSpreadsheetPrinterSettingsPart2Content(spreadsheetPrinterSettingsPart2);

            WorksheetPart worksheetPart3 = workbookPart1.AddNewPart<WorksheetPart>("rId1");
            GenerateWorksheetPart3Content(worksheetPart3);

            //SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart3 = worksheetPart3.AddNewPart<SpreadsheetPrinterSettingsPart>("rId1");
            //GenerateSpreadsheetPrinterSettingsPart3Content(spreadsheetPrinterSettingsPart3);

            ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>("rId6");
            GenerateThemePart1Content(themePart1);

            workbookPart1.AddPart(pivotTableCacheDefinitionPart1, "rId5");

            WorksheetPart worksheetPart4 = workbookPart1.AddNewPart<WorksheetPart>("rId4");
            generateTaskDataWorksheet(worksheetPart4);
            //GenerateWorksheetPart4Content(worksheetPart4);

            TableDefinitionPart tableDefinitionPart = worksheetPart4.AddNewPart<TableDefinitionPart>("rId2");
            generateTaskTableContent(tableDefinitionPart);
            //GenerateTableDefinitionPart1Content(tableDefinitionPart1);

            //SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart4 = worksheetPart4.AddNewPart<SpreadsheetPrinterSettingsPart>("rId1");
            //GenerateSpreadsheetPrinterSettingsPart4Content(spreadsheetPrinterSettingsPart4);

            CustomFilePropertiesPart customFilePropertiesPart1 = document.AddNewPart<CustomFilePropertiesPart>("rId5");
            GenerateCustomFilePropertiesPart1Content(customFilePropertiesPart1);

            ExtendedFilePropertiesPart extendedFilePropertiesPart1 = document.AddNewPart<ExtendedFilePropertiesPart>("rId4");
            GenerateExtendedFilePropertiesPart1Content(extendedFilePropertiesPart1);

            SetPackageProperties(document);
        }

        private void generateTaskDataWorksheet(WorksheetPart worksheetPart4)
        {
            foreach (KeyValuePair<string, userDetailsObject> person in reportData.people)
            {
                string displayName = person.Value.displayName;
                string userid = person.Key;
                Console.WriteLine(displayName);
                Console.WriteLine(userid);
            }
            Worksheet worksheet4 = new Worksheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac" } };
            worksheet4.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            worksheet4.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            worksheet4.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
            //worksheet4.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //worksheet4.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
            //worksheet4.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //worksheet4.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", genStrGuid()));
            SheetDimension sheetDimension4 = new SheetDimension() { Reference = "A1:J" + (reportData.tasks.value.Count + 1).ToString() };

            SheetViews sheetViews4 = new SheetViews();

            SheetView sheetView4 = new SheetView() { WorkbookViewId = (UInt32Value)0U };
            Selection selection2 = new Selection() { ActiveCell = "G23", SequenceOfReferences = new ListValue<StringValue>() { InnerText = "G23" } };

            sheetView4.Append(selection2);

            sheetViews4.Append(sheetView4);
            SheetFormatProperties sheetFormatProperties4 = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

            Columns columns4 = new Columns();
            Column column18 = new Column() { Min = (UInt32Value)1U, Max = (UInt32Value)1U, Width = 26.85546875D, BestFit = true, CustomWidth = true };
            Column column19 = new Column() { Min = (UInt32Value)2U, Max = (UInt32Value)2U, Width = 12.85546875D, BestFit = true, CustomWidth = true };
            Column column20 = new Column() { Min = (UInt32Value)3U, Max = (UInt32Value)3U, Width = 34.42578125D, BestFit = true, CustomWidth = true };
            Column column21 = new Column() { Min = (UInt32Value)4U, Max = (UInt32Value)4U, Width = 21.7109375D, BestFit = true, CustomWidth = true };
            Column column22 = new Column() { Min = (UInt32Value)5U, Max = (UInt32Value)5U, Width = 11.140625D, BestFit = true, CustomWidth = true };
            Column column23 = new Column() { Min = (UInt32Value)6U, Max = (UInt32Value)6U, Width = 15.7109375D, BestFit = true, CustomWidth = true };
            Column column24 = new Column() { Min = (UInt32Value)7U, Max = (UInt32Value)7U, Width = 18D, BestFit = true, CustomWidth = true };
            Column column25 = new Column() { Min = (UInt32Value)8U, Max = (UInt32Value)8U, Width = 16D, BestFit = true, CustomWidth = true };
            Column column26 = new Column() { Min = (UInt32Value)9U, Max = (UInt32Value)9U, Width = 15.5703125D, BestFit = true, CustomWidth = true };
            Column column27 = new Column() { Min = (UInt32Value)10U, Max = (UInt32Value)10U, Width = 35.85546875D, BestFit = true, CustomWidth = true };

            columns4.Append(column18);
            columns4.Append(column19);
            columns4.Append(column20);
            columns4.Append(column21);
            columns4.Append(column22);
            columns4.Append(column23);
            columns4.Append(column24);
            columns4.Append(column25);
            columns4.Append(column26);
            columns4.Append(column27);

            SheetData sheetData4 = new SheetData();

            Row headerRow = new Row() { RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            for (int i = 1; i <= headers.Count; i++)
            {
                Console.WriteLine("Header value: {0} ", headers[i - 1]);
                Console.WriteLine("Header cell ref: {0} ", rowArry[i - 1].ToString() + "1");


                Cell cellHeader = new Cell() { CellReference = rowArry[i - 1].ToString() + (1).ToString(), DataType = CellValues.InlineString };
                InlineString cellHeaderValue = new InlineString();

                cellHeaderValue.Text = new Text(headers[i - 1]);

                cellHeader.Append(cellHeaderValue);

                headerRow.Append(cellHeader);
            }

            sheetData4.Append(headerRow);

            for (int tIndex = 1; tIndex <= reportData.tasks.value.Count; tIndex++)
            {

                int iCol = 0;
                uint iRow = (uint)tIndex + 1;

                Row dataRow = new Row() { RowIndex = new UInt32Value((uint)iRow), Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

                taskObject task = reportData.tasks.value[tIndex - 1];

                Cell cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                //CellValue cellValue = new CellValue
                InlineString cellValue = new InlineString(new Text(task.title));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString();
                string userid = "";
                foreach (KeyValuePair<string, assignmentObject> pair in task.assignments)
                {
                    userid = pair.Key;
                    cellValue.Text = new Text(reportData.people[pair.Key].displayName);
                    break;
                }

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                string email = reportData.people[userid].mail;
                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString(new Text(email));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                string bucketName = "";
                foreach (bucketObject b in reportData.buckets)
                {
                    if (b.id == task.bucketId)
                        bucketName = b.name;
                }

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString(new Text(bucketName));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString();
                if (task.percentComplete == 0)
                    cellValue.Text = new Text("Not started");
                else
                    cellValue.Text = new Text(progress[100 / task.percentComplete]);

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString(new Text(getStringFromDate(task.dueDateTime)));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString(new Text(getStringFromDate(task.completedDateTime)));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                string completedByUser = " ";
                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                if ((task.completedBy != null) && (task.completedBy.user.id != null))
                    completedByUser = reportData.people[task.completedBy.user.id].displayName;
                cellValue = new InlineString(new Text(completedByUser));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString(new Text(getStringFromDate(task.createdDateTime)));

                cellData.Append(cellValue);
                dataRow.Append(cellData);

                cellData = new Cell() { CellReference = rowArry[iCol++].ToString() + (iRow).ToString(), DataType = CellValues.InlineString };
                cellValue = new InlineString(new Text(task.id));

                cellData.Append(cellValue);
                dataRow.Append(cellData);
                sheetData4.Append(dataRow);
                //Debugger.Break();
            }
            PageMargins pageMargins6 = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            PageSetup pageSetup6 = new PageSetup() { Orientation = OrientationValues.Portrait, Id = "rId1" };

            TableParts tableParts1 = new TableParts() { Count = (UInt32Value)1U };
            TablePart tablePart1 = new TablePart() { Id = "rId2" };

            tableParts1.Append(tablePart1);

            worksheet4.Append(sheetDimension4);
            worksheet4.Append(sheetViews4);
            worksheet4.Append(sheetFormatProperties4);
            worksheet4.Append(columns4);
            worksheet4.Append(sheetData4);
            worksheet4.Append(pageMargins6);
            worksheet4.Append(pageSetup6);
            worksheet4.Append(tableParts1);

            worksheetPart4.Worksheet = worksheet4;
        }


        private void generateTaskTableContent(TableDefinitionPart tableDefinitionPart)
        {
            Table table = new Table() { Id = (UInt32Value)4U, Name = "WorkItemsTable", DisplayName = "WorkItemsTable", Reference = "A1:J" + (reportData.tasks.value.Count + 1).ToString(), TotalsRowShown = false };
            table.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //table1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //table1.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //table1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{5EFA48B8-3293-4748-AA3B-548E4EF8F199}"));

            AutoFilter autoFilter1 = new AutoFilter() { Reference = "A1:J" + (reportData.tasks.value.Count + 1).ToString() };
            //autoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{EDA87F4E-B12B-41C9-8B20-4850D8E9E195}"));

            TableColumns tableColumns = new TableColumns() { Count = (uint)headers.Count };

            for (int i = 1; i <= headers.Count; i++)
            {

                TableColumn tableColumn = new TableColumn() { Id = (uint)i, Name = headers[i - 1] };
                //tableColumn.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{D2D20073-D29B-4500-9950-65429DC6D749}"));
                Console.WriteLine("Header value: {0} ", headers[i - 1]);
                Console.WriteLine("Header cell ref: {0} ", rowArry[i - 1].ToString() + "1");

                tableColumns.Append(tableColumn);
            }

            TableStyleInfo tableStyleInfo = new TableStyleInfo() { Name = "TableStyleMedium2", ShowFirstColumn = false, ShowLastColumn = false, ShowRowStripes = true, ShowColumnStripes = false };

            table.Append(autoFilter1);
            table.Append(tableColumns);
            table.Append(tableStyleInfo);

            tableDefinitionPart.Table = table;
        }

        public static string genStrGuid()
        {
            return "{" + Guid.NewGuid() + "}";
        }

        public string getStringFromDate(string dateStr)
        {
            DateTime date1970 = new DateTime(1969, 12, 31, 23, 59, 59);
            string retDate = "";
            if (dateStr != null)
            {
                DateTime date = DateTime.Parse(dateStr);
                if (date.Year > date1970.Year)
                    retDate = date.ToString("d");
            }
            return retDate;
        }

        private void generateCoverDrawingsContent(DrawingsPart drawingsPart1)
        {
            Xdr.WorksheetDrawing worksheetDrawing1 = new Xdr.WorksheetDrawing();
            worksheetDrawing1.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
            worksheetDrawing1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = "0";
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = "519112";
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = "13";
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = "38100";

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = "5";
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = "366712";
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = "27";
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = "114300";

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.GraphicFrame graphicFrame1 = new Xdr.GraphicFrame() { Macro = "" };

            Xdr.NonVisualGraphicFrameProperties nonVisualGraphicFrameProperties1 = new Xdr.NonVisualGraphicFrameProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "Chart 1" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList1 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension1 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement6 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{5E79015C-1433-4854-B048-179923F8BEE9}\" />");

            nonVisualDrawingPropertiesExtension1.Append(openXmlUnknownElement6);

            nonVisualDrawingPropertiesExtensionList1.Append(nonVisualDrawingPropertiesExtension1);

            nonVisualDrawingProperties1.Append(nonVisualDrawingPropertiesExtensionList1);
            Xdr.NonVisualGraphicFrameDrawingProperties nonVisualGraphicFrameDrawingProperties1 = new Xdr.NonVisualGraphicFrameDrawingProperties();

            nonVisualGraphicFrameProperties1.Append(nonVisualDrawingProperties1);
            nonVisualGraphicFrameProperties1.Append(nonVisualGraphicFrameDrawingProperties1);

            Xdr.Transform transform1 = new Xdr.Transform();
            A.Offset offset1 = new A.Offset() { X = 0L, Y = 0L };
            A.Extents extents1 = new A.Extents() { Cx = 0L, Cy = 0L };

            transform1.Append(offset1);
            transform1.Append(extents1);

            A.Graphic graphic1 = new A.Graphic();

            A.GraphicData graphicData1 = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" };

            C.ChartReference chartReference1 = new C.ChartReference() { Id = "rId1" };
            chartReference1.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
            chartReference1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            graphicData1.Append(chartReference1);

            graphic1.Append(graphicData1);

            graphicFrame1.Append(nonVisualGraphicFrameProperties1);
            graphicFrame1.Append(transform1);
            graphicFrame1.Append(graphic1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(graphicFrame1);
            twoCellAnchor1.Append(clientData1);

            Xdr.TwoCellAnchor twoCellAnchor2 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker2 = new Xdr.FromMarker();
            Xdr.ColumnId columnId3 = new Xdr.ColumnId();
            columnId3.Text = "6";
            Xdr.ColumnOffset columnOffset3 = new Xdr.ColumnOffset();
            columnOffset3.Text = "228600";
            Xdr.RowId rowId3 = new Xdr.RowId();
            rowId3.Text = "13";
            Xdr.RowOffset rowOffset3 = new Xdr.RowOffset();
            rowOffset3.Text = "57150";

            fromMarker2.Append(columnId3);
            fromMarker2.Append(columnOffset3);
            fromMarker2.Append(rowId3);
            fromMarker2.Append(rowOffset3);

            Xdr.ToMarker toMarker2 = new Xdr.ToMarker();
            Xdr.ColumnId columnId4 = new Xdr.ColumnId();
            columnId4.Text = "11";
            Xdr.ColumnOffset columnOffset4 = new Xdr.ColumnOffset();
            columnOffset4.Text = "704850";
            Xdr.RowId rowId4 = new Xdr.RowId();
            rowId4.Text = "27";
            Xdr.RowOffset rowOffset4 = new Xdr.RowOffset();
            rowOffset4.Text = "133350";

            toMarker2.Append(columnId4);
            toMarker2.Append(columnOffset4);
            toMarker2.Append(rowId4);
            toMarker2.Append(rowOffset4);

            Xdr.GraphicFrame graphicFrame2 = new Xdr.GraphicFrame() { Macro = "" };

            Xdr.NonVisualGraphicFrameProperties nonVisualGraphicFrameProperties2 = new Xdr.NonVisualGraphicFrameProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties2 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)3U, Name = "Chart 2" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList2 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension2 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement7 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{71C390B4-509C-43A4-BD69-AC638451F404}\" />");

            nonVisualDrawingPropertiesExtension2.Append(openXmlUnknownElement7);

            nonVisualDrawingPropertiesExtensionList2.Append(nonVisualDrawingPropertiesExtension2);

            nonVisualDrawingProperties2.Append(nonVisualDrawingPropertiesExtensionList2);
            Xdr.NonVisualGraphicFrameDrawingProperties nonVisualGraphicFrameDrawingProperties2 = new Xdr.NonVisualGraphicFrameDrawingProperties();

            nonVisualGraphicFrameProperties2.Append(nonVisualDrawingProperties2);
            nonVisualGraphicFrameProperties2.Append(nonVisualGraphicFrameDrawingProperties2);

            Xdr.Transform transform2 = new Xdr.Transform();
            A.Offset offset2 = new A.Offset() { X = 0L, Y = 0L };
            A.Extents extents2 = new A.Extents() { Cx = 0L, Cy = 0L };

            transform2.Append(offset2);
            transform2.Append(extents2);

            A.Graphic graphic2 = new A.Graphic();

            A.GraphicData graphicData2 = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" };

            C.ChartReference chartReference2 = new C.ChartReference() { Id = "rId2" };
            chartReference2.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
            chartReference2.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            graphicData2.Append(chartReference2);

            graphic2.Append(graphicData2);

            graphicFrame2.Append(nonVisualGraphicFrameProperties2);
            graphicFrame2.Append(transform2);
            graphicFrame2.Append(graphic2);
            Xdr.ClientData clientData2 = new Xdr.ClientData();

            twoCellAnchor2.Append(fromMarker2);
            twoCellAnchor2.Append(toMarker2);
            twoCellAnchor2.Append(graphicFrame2);
            twoCellAnchor2.Append(clientData2);

            Xdr.TwoCellAnchor twoCellAnchor3 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker3 = new Xdr.FromMarker();
            Xdr.ColumnId columnId5 = new Xdr.ColumnId();
            columnId5.Text = "0";
            Xdr.ColumnOffset columnOffset5 = new Xdr.ColumnOffset();
            columnOffset5.Text = "514350";
            Xdr.RowId rowId5 = new Xdr.RowId();
            rowId5.Text = "1";
            Xdr.RowOffset rowOffset5 = new Xdr.RowOffset();
            rowOffset5.Text = "152400";

            fromMarker3.Append(columnId5);
            fromMarker3.Append(columnOffset5);
            fromMarker3.Append(rowId5);
            fromMarker3.Append(rowOffset5);

            Xdr.ToMarker toMarker3 = new Xdr.ToMarker();
            Xdr.ColumnId columnId6 = new Xdr.ColumnId();
            columnId6.Text = "3";
            Xdr.ColumnOffset columnOffset6 = new Xdr.ColumnOffset();
            columnOffset6.Text = "390526";
            Xdr.RowId rowId6 = new Xdr.RowId();
            rowId6.Text = "4";
            Xdr.RowOffset rowOffset6 = new Xdr.RowOffset();
            rowOffset6.Text = "38100";

            toMarker3.Append(columnId6);
            toMarker3.Append(columnOffset6);
            toMarker3.Append(rowId6);
            toMarker3.Append(rowOffset6);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)4U, Name = "TextBox 3" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList3 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension3 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement8 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{D6E05017-EE1F-4927-8E1A-EBB00EB149E6}\" />");

            nonVisualDrawingPropertiesExtension3.Append(openXmlUnknownElement8);

            nonVisualDrawingPropertiesExtensionList3.Append(nonVisualDrawingPropertiesExtension3);

            nonVisualDrawingProperties3.Append(nonVisualDrawingPropertiesExtensionList3);
            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties();

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset3 = new A.Offset() { X = 514350L, Y = 342900L };
            A.Extents extents3 = new A.Extents() { Cx = 3114676L, Cy = 457200L };

            transform2D1.Append(offset3);
            transform2D1.Append(extents3);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill1 = new A.SolidFill();
            A.SchemeColor schemeColor1 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill1.Append(schemeColor1);

            A.Outline outline1 = new A.Outline() { Width = 9525, CompoundLineType = A.CompoundLineValues.Single };

            A.SolidFill solidFill2 = new A.SolidFill();

            A.SchemeColor schemeColor2 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };
            A.Shade shade1 = new A.Shade() { Val = 50000 };

            schemeColor2.Append(shade1);

            solidFill2.Append(schemeColor2);

            outline1.Append(solidFill2);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill1);
            shapeProperties1.Append(outline1);

            Xdr.ShapeStyle shapeStyle1 = new Xdr.ShapeStyle();

            A.LineReference lineReference1 = new A.LineReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage1 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            lineReference1.Append(rgbColorModelPercentage1);

            A.FillReference fillReference1 = new A.FillReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage2 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            fillReference1.Append(rgbColorModelPercentage2);

            A.EffectReference effectReference1 = new A.EffectReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage3 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            effectReference1.Append(rgbColorModelPercentage3);

            A.FontReference fontReference1 = new A.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor3 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };

            fontReference1.Append(schemeColor3);

            shapeStyle1.Append(lineReference1);
            shapeStyle1.Append(fillReference1);
            shapeStyle1.Append(effectReference1);
            shapeStyle1.Append(fontReference1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();
            A.BodyProperties bodyProperties1 = new A.BodyProperties() { VerticalOverflow = A.TextVerticalOverflowValues.Clip, HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, Wrap = A.TextWrappingValues.Square, RightToLeftColumns = false, Anchor = A.TextAnchoringTypeValues.Top };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.Run run1 = new A.Run();
            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", FontSize = 1800 };
            A.Text text55 = new A.Text();
            string bucketName = "";
            foreach (bucketObject b in reportData.buckets)
            {
                if (b.id == reportData.bucketId)
                    bucketName = b.name;
            }
            text55.Text = "Focus Report for " + bucketName;

            run1.Append(runProperties1);
            run1.Append(text55);

            A.Run run2 = new A.Run();
            A.RunProperties runProperties2 = new A.RunProperties() { Language = "en-US", FontSize = 1800, Baseline = 0 };
            A.Text text56 = new A.Text();
            text56.Text = " Bucket";

            run2.Append(runProperties2);
            run2.Append(text56);
            A.EndParagraphRunProperties endParagraphRunProperties1 = new A.EndParagraphRunProperties() { Language = "en-US", FontSize = 1800 };

            paragraph1.Append(run1);
            paragraph1.Append(run2);
            paragraph1.Append(endParagraphRunProperties1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(shapeStyle1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData3 = new Xdr.ClientData();

            twoCellAnchor3.Append(fromMarker3);
            twoCellAnchor3.Append(toMarker3);
            twoCellAnchor3.Append(shape1);
            twoCellAnchor3.Append(clientData3);

            Xdr.TwoCellAnchor twoCellAnchor4 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker4 = new Xdr.FromMarker();
            Xdr.ColumnId columnId7 = new Xdr.ColumnId();
            columnId7.Text = "0";
            Xdr.ColumnOffset columnOffset7 = new Xdr.ColumnOffset();
            columnOffset7.Text = "495300";
            Xdr.RowId rowId7 = new Xdr.RowId();
            rowId7.Text = "4";
            Xdr.RowOffset rowOffset7 = new Xdr.RowOffset();
            rowOffset7.Text = "95250";

            fromMarker4.Append(columnId7);
            fromMarker4.Append(columnOffset7);
            fromMarker4.Append(rowId7);
            fromMarker4.Append(rowOffset7);

            Xdr.ToMarker toMarker4 = new Xdr.ToMarker();
            Xdr.ColumnId columnId8 = new Xdr.ColumnId();
            columnId8.Text = "1";
            Xdr.ColumnOffset columnOffset8 = new Xdr.ColumnOffset();
            columnOffset8.Text = "942975";
            Xdr.RowId rowId8 = new Xdr.RowId();
            rowId8.Text = "6";
            Xdr.RowOffset rowOffset8 = new Xdr.RowOffset();
            rowOffset8.Text = "171450";

            toMarker4.Append(columnId8);
            toMarker4.Append(columnOffset8);
            toMarker4.Append(rowId8);
            toMarker4.Append(rowOffset8);

            Xdr.Shape shape2 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties2 = new Xdr.NonVisualShapeProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties4 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)5U, Name = "TextBox 4" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList4 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension4 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement9 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{8B0838B4-7F61-47ED-BDEC-9B0B7DBBFE58}\" />");

            nonVisualDrawingPropertiesExtension4.Append(openXmlUnknownElement9);

            nonVisualDrawingPropertiesExtensionList4.Append(nonVisualDrawingPropertiesExtension4);

            nonVisualDrawingProperties4.Append(nonVisualDrawingPropertiesExtensionList4);
            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties2 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };

            nonVisualShapeProperties2.Append(nonVisualDrawingProperties4);
            nonVisualShapeProperties2.Append(nonVisualShapeDrawingProperties2);

            Xdr.ShapeProperties shapeProperties2 = new Xdr.ShapeProperties();

            A.Transform2D transform2D2 = new A.Transform2D();
            A.Offset offset4 = new A.Offset() { X = 495300L, Y = 857250L };
            A.Extents extents4 = new A.Extents() { Cx = 1885950L, Cy = 457200L };

            transform2D2.Append(offset4);
            transform2D2.Append(extents4);

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList2 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList2);

            A.SolidFill solidFill3 = new A.SolidFill();
            A.SchemeColor schemeColor4 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill3.Append(schemeColor4);

            A.Outline outline2 = new A.Outline() { Width = 9525, CompoundLineType = A.CompoundLineValues.Single };

            A.SolidFill solidFill4 = new A.SolidFill();

            A.SchemeColor schemeColor5 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };
            A.Shade shade2 = new A.Shade() { Val = 50000 };

            schemeColor5.Append(shade2);

            solidFill4.Append(schemeColor5);

            outline2.Append(solidFill4);

            shapeProperties2.Append(transform2D2);
            shapeProperties2.Append(presetGeometry2);
            shapeProperties2.Append(solidFill3);
            shapeProperties2.Append(outline2);

            Xdr.ShapeStyle shapeStyle2 = new Xdr.ShapeStyle();

            A.LineReference lineReference2 = new A.LineReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage4 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            lineReference2.Append(rgbColorModelPercentage4);

            A.FillReference fillReference2 = new A.FillReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage5 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            fillReference2.Append(rgbColorModelPercentage5);

            A.EffectReference effectReference2 = new A.EffectReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage6 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            effectReference2.Append(rgbColorModelPercentage6);

            A.FontReference fontReference2 = new A.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor6 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };

            fontReference2.Append(schemeColor6);

            shapeStyle2.Append(lineReference2);
            shapeStyle2.Append(fillReference2);
            shapeStyle2.Append(effectReference2);
            shapeStyle2.Append(fontReference2);

            Xdr.TextBody textBody2 = new Xdr.TextBody();
            A.BodyProperties bodyProperties2 = new A.BodyProperties() { VerticalOverflow = A.TextVerticalOverflowValues.Clip, HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, Wrap = A.TextWrappingValues.Square, RightToLeftColumns = false, Anchor = A.TextAnchoringTypeValues.Top };
            A.ShapeAutoFit shapeAutoFit2 = new A.ShapeAutoFit();

            bodyProperties2.Append(shapeAutoFit2);
            A.ListStyle listStyle2 = new A.ListStyle();

            A.Paragraph paragraph2 = new A.Paragraph();

            A.Run run3 = new A.Run();
            A.RunProperties runProperties3 = new A.RunProperties() { Language = "en-US", FontSize = 1800 };
            A.Text text57 = new A.Text();
            //text57.Text = "October 8, 2019";
            text57.Text = DateTime.Today.ToString("d");

            run3.Append(runProperties3);
            run3.Append(text57);

            paragraph2.Append(run3);

            textBody2.Append(bodyProperties2);
            textBody2.Append(listStyle2);
            textBody2.Append(paragraph2);

            shape2.Append(nonVisualShapeProperties2);
            shape2.Append(shapeProperties2);
            shape2.Append(shapeStyle2);
            shape2.Append(textBody2);
            Xdr.ClientData clientData4 = new Xdr.ClientData();

            twoCellAnchor4.Append(fromMarker4);
            twoCellAnchor4.Append(toMarker4);
            twoCellAnchor4.Append(shape2);
            twoCellAnchor4.Append(clientData4);

            Xdr.TwoCellAnchor twoCellAnchor5 = new Xdr.TwoCellAnchor() { EditAs = Xdr.EditAsValues.OneCell };

            Xdr.FromMarker fromMarker5 = new Xdr.FromMarker();
            Xdr.ColumnId columnId9 = new Xdr.ColumnId();
            columnId9.Text = "6";
            Xdr.ColumnOffset columnOffset9 = new Xdr.ColumnOffset();
            columnOffset9.Text = "571500";
            Xdr.RowId rowId9 = new Xdr.RowId();
            rowId9.Text = "1";
            Xdr.RowOffset rowOffset9 = new Xdr.RowOffset();
            rowOffset9.Text = "133350";

            fromMarker5.Append(columnId9);
            fromMarker5.Append(columnOffset9);
            fromMarker5.Append(rowId9);
            fromMarker5.Append(rowOffset9);

            Xdr.ToMarker toMarker5 = new Xdr.ToMarker();
            Xdr.ColumnId columnId10 = new Xdr.ColumnId();
            columnId10.Text = "9";
            Xdr.ColumnOffset columnOffset10 = new Xdr.ColumnOffset();
            columnOffset10.Text = "419100";
            Xdr.RowId rowId10 = new Xdr.RowId();
            rowId10.Text = "11";
            Xdr.RowOffset rowOffset10 = new Xdr.RowOffset();
            rowOffset10.Text = "95250";

            toMarker5.Append(columnId10);
            toMarker5.Append(columnOffset10);
            toMarker5.Append(rowId10);
            toMarker5.Append(rowOffset10);

            Xdr.Picture picture1 = new Xdr.Picture();

            Xdr.NonVisualPictureProperties nonVisualPictureProperties1 = new Xdr.NonVisualPictureProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties5 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)7U, Name = "Picture 6" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList5 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension5 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement10 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{58903F66-BAC7-4B6C-8A4A-C10CD0B6502F}\" />");

            nonVisualDrawingPropertiesExtension5.Append(openXmlUnknownElement10);

            nonVisualDrawingPropertiesExtensionList5.Append(nonVisualDrawingPropertiesExtension5);

            nonVisualDrawingProperties5.Append(nonVisualDrawingPropertiesExtensionList5);

            Xdr.NonVisualPictureDrawingProperties nonVisualPictureDrawingProperties1 = new Xdr.NonVisualPictureDrawingProperties();
            A.PictureLocks pictureLocks1 = new A.PictureLocks() { NoChangeAspect = true };

            nonVisualPictureDrawingProperties1.Append(pictureLocks1);

            nonVisualPictureProperties1.Append(nonVisualDrawingProperties5);
            nonVisualPictureProperties1.Append(nonVisualPictureDrawingProperties1);

            Xdr.BlipFill blipFill1 = new Xdr.BlipFill();

            A.Blip blip1 = new A.Blip() { Embed = "rId3" };
            blip1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            A.BlipExtensionList blipExtensionList1 = new A.BlipExtensionList();

            A.BlipExtension blipExtension1 = new A.BlipExtension() { Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}" };

            A14.UseLocalDpi useLocalDpi1 = new A14.UseLocalDpi() { Val = false };
            useLocalDpi1.AddNamespaceDeclaration("a14", "http://schemas.microsoft.com/office/drawing/2010/main");

            blipExtension1.Append(useLocalDpi1);

            blipExtensionList1.Append(blipExtension1);

            blip1.Append(blipExtensionList1);

            A.Stretch stretch1 = new A.Stretch();
            A.FillRectangle fillRectangle1 = new A.FillRectangle();

            stretch1.Append(fillRectangle1);

            blipFill1.Append(blip1);
            blipFill1.Append(stretch1);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties();

            A.Transform2D transform2D3 = new A.Transform2D();
            A.Offset offset5 = new A.Offset() { X = 5905500L, Y = 323850L };
            A.Extents extents5 = new A.Extents() { Cx = 2190750L, Cy = 1866900L };

            transform2D3.Append(offset5);
            transform2D3.Append(extents5);

            A.PresetGeometry presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);

            picture1.Append(nonVisualPictureProperties1);
            picture1.Append(blipFill1);
            picture1.Append(shapeProperties3);
            Xdr.ClientData clientData5 = new Xdr.ClientData();

            twoCellAnchor5.Append(fromMarker5);
            twoCellAnchor5.Append(toMarker5);
            twoCellAnchor5.Append(picture1);
            twoCellAnchor5.Append(clientData5);

            Xdr.TwoCellAnchor twoCellAnchor6 = new Xdr.TwoCellAnchor() { EditAs = Xdr.EditAsValues.OneCell };

            Xdr.FromMarker fromMarker6 = new Xdr.FromMarker();
            Xdr.ColumnId columnId11 = new Xdr.ColumnId();
            columnId11.Text = "13";
            Xdr.ColumnOffset columnOffset11 = new Xdr.ColumnOffset();
            columnOffset11.Text = "238125";
            Xdr.RowId rowId11 = new Xdr.RowId();
            rowId11.Text = "0";
            Xdr.RowOffset rowOffset11 = new Xdr.RowOffset();
            rowOffset11.Text = "57150";

            fromMarker6.Append(columnId11);
            fromMarker6.Append(columnOffset11);
            fromMarker6.Append(rowId11);
            fromMarker6.Append(rowOffset11);

            Xdr.ToMarker toMarker6 = new Xdr.ToMarker();
            Xdr.ColumnId columnId12 = new Xdr.ColumnId();
            columnId12.Text = "28";
            Xdr.ColumnOffset columnOffset12 = new Xdr.ColumnOffset();
            columnOffset12.Text = "76200";
            Xdr.RowId rowId12 = new Xdr.RowId();
            rowId12.Text = "30";
            Xdr.RowOffset rowOffset12 = new Xdr.RowOffset();
            rowOffset12.Text = "0";

            toMarker6.Append(columnId12);
            toMarker6.Append(columnOffset12);
            toMarker6.Append(rowId12);
            toMarker6.Append(rowOffset12);

            Xdr.Picture picture2 = new Xdr.Picture();

            Xdr.NonVisualPictureProperties nonVisualPictureProperties2 = new Xdr.NonVisualPictureProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties6 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)9U, Name = "Picture 8" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList6 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension6 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement11 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{89F921A3-8E18-4FF1-A28F-919328831693}\" />");

            nonVisualDrawingPropertiesExtension6.Append(openXmlUnknownElement11);

            nonVisualDrawingPropertiesExtensionList6.Append(nonVisualDrawingPropertiesExtension6);

            nonVisualDrawingProperties6.Append(nonVisualDrawingPropertiesExtensionList6);

            Xdr.NonVisualPictureDrawingProperties nonVisualPictureDrawingProperties2 = new Xdr.NonVisualPictureDrawingProperties();
            A.PictureLocks pictureLocks2 = new A.PictureLocks() { NoChangeAspect = true };

            nonVisualPictureDrawingProperties2.Append(pictureLocks2);

            nonVisualPictureProperties2.Append(nonVisualDrawingProperties6);
            nonVisualPictureProperties2.Append(nonVisualPictureDrawingProperties2);

            Xdr.BlipFill blipFill2 = new Xdr.BlipFill();

            A.Blip blip2 = new A.Blip() { Embed = "rId4" };
            blip2.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            A.BlipExtensionList blipExtensionList2 = new A.BlipExtensionList();

            A.BlipExtension blipExtension2 = new A.BlipExtension() { Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}" };

            A14.UseLocalDpi useLocalDpi2 = new A14.UseLocalDpi() { Val = false };
            useLocalDpi2.AddNamespaceDeclaration("a14", "http://schemas.microsoft.com/office/drawing/2010/main");

            blipExtension2.Append(useLocalDpi2);

            blipExtensionList2.Append(blipExtension2);

            blip2.Append(blipExtensionList2);

            A.Stretch stretch2 = new A.Stretch();
            A.FillRectangle fillRectangle2 = new A.FillRectangle();

            stretch2.Append(fillRectangle2);

            blipFill2.Append(blip2);
            blipFill2.Append(stretch2);

            Xdr.ShapeProperties shapeProperties4 = new Xdr.ShapeProperties();

            A.Transform2D transform2D4 = new A.Transform2D();
            A.Offset offset6 = new A.Offset() { X = 11772900L, Y = 57150L };
            A.Extents extents6 = new A.Extents() { Cx = 10058400L, Cy = 5657850L };

            transform2D4.Append(offset6);
            transform2D4.Append(extents6);

            A.PresetGeometry presetGeometry4 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList4 = new A.AdjustValueList();

            presetGeometry4.Append(adjustValueList4);

            shapeProperties4.Append(transform2D4);
            shapeProperties4.Append(presetGeometry4);

            picture2.Append(nonVisualPictureProperties2);
            picture2.Append(blipFill2);
            picture2.Append(shapeProperties4);
            Xdr.ClientData clientData6 = new Xdr.ClientData();

            twoCellAnchor6.Append(fromMarker6);
            twoCellAnchor6.Append(toMarker6);
            twoCellAnchor6.Append(picture2);
            twoCellAnchor6.Append(clientData6);

            worksheetDrawing1.Append(twoCellAnchor1);
            worksheetDrawing1.Append(twoCellAnchor2);
            worksheetDrawing1.Append(twoCellAnchor3);
            worksheetDrawing1.Append(twoCellAnchor4);
            worksheetDrawing1.Append(twoCellAnchor5);
            worksheetDrawing1.Append(twoCellAnchor6);

            drawingsPart1.WorksheetDrawing = worksheetDrawing1;
        }
        private void generateCoverPivotTableCacheDefinitionContent(PivotTableCacheDefinitionPart pivotTableCacheDefinitionPart1)
        {
            PivotCacheDefinition pivotCacheDefinition1 = new PivotCacheDefinition() { Id = "rId1", RefreshOnLoad = true, RefreshedBy = "Tom Jebo", RefreshedDate = 43747.681090162034D, CreatedVersion = 6, RefreshedVersion = 6, MinRefreshableVersion = 3, RecordCount = (UInt32Value)7U };
            pivotCacheDefinition1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            pivotCacheDefinition1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //pivotCacheDefinition1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //pivotCacheDefinition1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{EB244034-01A9-4680-A3DF-D8D769D5F69B}"));

            CacheSource cacheSource1 = new CacheSource() { Type = SourceValues.Worksheet };
            WorksheetSource worksheetSource1 = new WorksheetSource() { Name = "WorkItemsTable" };

            cacheSource1.Append(worksheetSource1);

            CacheFields cacheFields1 = new CacheFields() { Count = (UInt32Value)10U };

            CacheField cacheField1 = new CacheField() { Name = "Task", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems1 = new SharedItems();

            cacheField1.Append(sharedItems1);

            CacheField cacheField2 = new CacheField() { Name = "Owner", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems2 = new SharedItems();

            cacheField2.Append(sharedItems2);

            CacheField cacheField3 = new CacheField() { Name = "Email", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems3 = new SharedItems();

            cacheField3.Append(sharedItems3);

            CacheField cacheField4 = new CacheField() { Name = "Bucket", NumberFormatId = (UInt32Value)0U };

            SharedItems sharedItems4 = new SharedItems() { Count = (UInt32Value)3U };
            StringItem stringItem1 = new StringItem() { Val = "ChemCo Distilling Visit" };
            StringItem stringItem2 = new StringItem() { Val = "Redmond Site Visit" };
            StringItem stringItem3 = new StringItem() { Val = "Focus Bucket" };

            sharedItems4.Append(stringItem1);
            sharedItems4.Append(stringItem2);
            sharedItems4.Append(stringItem3);

            cacheField4.Append(sharedItems4);

            CacheField cacheField5 = new CacheField() { Name = "Progress", NumberFormatId = (UInt32Value)0U };

            SharedItems sharedItems5 = new SharedItems() { Count = (UInt32Value)3U };
            StringItem stringItem4 = new StringItem() { Val = "Not started" };
            StringItem stringItem5 = new StringItem() { Val = "In Progress" };
            StringItem stringItem6 = new StringItem() { Val = "Completed" };

            sharedItems5.Append(stringItem4);
            sharedItems5.Append(stringItem5);
            sharedItems5.Append(stringItem6);

            cacheField5.Append(sharedItems5);

            CacheField cacheField6 = new CacheField() { Name = "Due Date", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems6 = new SharedItems();

            cacheField6.Append(sharedItems6);

            CacheField cacheField7 = new CacheField() { Name = "Completed Date", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems7 = new SharedItems() { ContainsBlank = true };

            cacheField7.Append(sharedItems7);

            CacheField cacheField8 = new CacheField() { Name = "Completed By", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems8 = new SharedItems() { ContainsNonDate = false, ContainsString = false, ContainsBlank = true };

            cacheField8.Append(sharedItems8);

            CacheField cacheField9 = new CacheField() { Name = "Created Date", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems9 = new SharedItems();

            cacheField9.Append(sharedItems9);

            CacheField cacheField10 = new CacheField() { Name = "Task Id", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems10 = new SharedItems();

            cacheField10.Append(sharedItems10);

            cacheFields1.Append(cacheField1);
            cacheFields1.Append(cacheField2);
            cacheFields1.Append(cacheField3);
            cacheFields1.Append(cacheField4);
            cacheFields1.Append(cacheField5);
            cacheFields1.Append(cacheField6);
            cacheFields1.Append(cacheField7);
            cacheFields1.Append(cacheField8);
            cacheFields1.Append(cacheField9);
            cacheFields1.Append(cacheField10);

            PivotCacheDefinitionExtensionList pivotCacheDefinitionExtensionList1 = new PivotCacheDefinitionExtensionList();

            PivotCacheDefinitionExtension pivotCacheDefinitionExtension1 = new PivotCacheDefinitionExtension() { Uri = "{725AE2AE-9491-48be-B2B4-4EB974FC3084}" };
            pivotCacheDefinitionExtension1.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
            X14.PivotCacheDefinition pivotCacheDefinition2 = new X14.PivotCacheDefinition();

            pivotCacheDefinitionExtension1.Append(pivotCacheDefinition2);

            pivotCacheDefinitionExtensionList1.Append(pivotCacheDefinitionExtension1);

            pivotCacheDefinition1.Append(cacheSource1);
            pivotCacheDefinition1.Append(cacheFields1);
            pivotCacheDefinition1.Append(pivotCacheDefinitionExtensionList1);

            pivotTableCacheDefinitionPart1.PivotCacheDefinition = pivotCacheDefinition1;
        }
        // Generates content of webExTaskpanesPart1.
        private void GenerateWebExTaskpanesPart1Content(WebExTaskpanesPart webExTaskpanesPart1)
        {
            Wetp.Taskpanes taskpanes1 = new Wetp.Taskpanes();
            taskpanes1.AddNamespaceDeclaration("wetp", "http://schemas.microsoft.com/office/webextensions/taskpanes/2010/11");

            Wetp.WebExtensionTaskpane webExtensionTaskpane1 = new Wetp.WebExtensionTaskpane() { DockState = "right", Visibility = false, Width = 350D, Row = (UInt32Value)5U };

            Wetp.WebExtensionPartReference webExtensionPartReference1 = new Wetp.WebExtensionPartReference() { Id = "rId1" };
            webExtensionPartReference1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            webExtensionTaskpane1.Append(webExtensionPartReference1);

            taskpanes1.Append(webExtensionTaskpane1);

            webExTaskpanesPart1.Taskpanes = taskpanes1;
        }

        // Generates content of webExtensionPart1.
        private void GenerateWebExtensionPart1Content(WebExtensionPart webExtensionPart1)
        {
            We.WebExtension webExtension1 = new We.WebExtension() { Id = "{7988B218-5CA4-4DF4-86B1-D987A3531C56}" };
            webExtension1.AddNamespaceDeclaration("we", "http://schemas.microsoft.com/office/webextensions/webextension/2010/11");
            We.WebExtensionStoreReference webExtensionStoreReference1 = new We.WebExtensionStoreReference() { Id = "05c2e1c9-3e1d-406e-9a91-e9ac64854143", Version = "1.0.0.0", Store = "developer", StoreType = "uploadfiledevcatalog" };

            We.WebExtensionReferenceList webExtensionReferenceList1 = new We.WebExtensionReferenceList();
            We.WebExtensionStoreReference webExtensionStoreReference2 = new We.WebExtensionStoreReference() { Id = "05c2e1c9-3e1d-406e-9a91-e9ac64854143", Version = "1.0.0.0", Store = "omex", StoreType = "omex" };

            webExtensionReferenceList1.Append(webExtensionStoreReference2);

            We.WebExtensionPropertyBag webExtensionPropertyBag1 = new We.WebExtensionPropertyBag();
            We.WebExtensionProperty webExtensionProperty1 = new We.WebExtensionProperty() { Name = "Office.AutoShowTaskpaneWithDocument", Value = "true" };

            webExtensionPropertyBag1.Append(webExtensionProperty1);
            We.WebExtensionBindingList webExtensionBindingList1 = new We.WebExtensionBindingList();

            We.Snapshot snapshot1 = new We.Snapshot();
            snapshot1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            We.OfficeArtExtensionList officeArtExtensionList1 = new We.OfficeArtExtensionList();

            A.Extension extension1 = new A.Extension() { Uri = "{D87F86FE-615C-45B5-9D79-34F1136793EB}" };
            extension1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            OpenXmlUnknownElement openXmlUnknownElement1 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<we:containsCustomFunctions xmlns:we=\"http://schemas.microsoft.com/office/webextensions/webextension/2010/11\" />");

            extension1.Append(openXmlUnknownElement1);

            officeArtExtensionList1.Append(extension1);

            webExtension1.Append(webExtensionStoreReference1);
            webExtension1.Append(webExtensionReferenceList1);
            webExtension1.Append(webExtensionPropertyBag1);
            webExtension1.Append(webExtensionBindingList1);
            webExtension1.Append(snapshot1);
            webExtension1.Append(officeArtExtensionList1);

            webExtensionPart1.WebExtension = webExtension1;
        }

        // Generates content of workbookPart1.
        private void GenerateWorkbookPart1Content(WorkbookPart workbookPart1)
        {
            DocumentFormat.OpenXml.Spreadsheet.Workbook workbook1 = new DocumentFormat.OpenXml.Spreadsheet.Workbook() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x15" } };
            workbook1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            workbook1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            workbook1.AddNamespaceDeclaration("x15", "http://schemas.microsoft.com/office/spreadsheetml/2010/11/main");
            ////workbook1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            ////workbook1.AddNamespaceDeclaration("xr6", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision6");
            ////workbook1.AddNamespaceDeclaration("xr10", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision10");
            ////workbook1.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
            FileVersion fileVersion1 = new FileVersion() { ApplicationName = "xl", LastEdited = "7", LowestEdited = "7", BuildVersion = "22130" };
            WorkbookProperties workbookProperties1 = new WorkbookProperties() { DefaultThemeVersion = (UInt32Value)166925U };

            AlternateContent alternateContent1 = new AlternateContent();
            alternateContent1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");

            AlternateContentChoice alternateContentChoice1 = new AlternateContentChoice() { Requires = "x15" };

            X15ac.AbsolutePath absolutePath1 = new X15ac.AbsolutePath() { Url = "C:\\Users\\tomjebo\\Documents\\" };
            absolutePath1.AddNamespaceDeclaration("x15ac", "http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac");

            alternateContentChoice1.Append(absolutePath1);

            alternateContent1.Append(alternateContentChoice1);

            //OpenXmlUnknownElement openXmlUnknownElement2 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<xr:revisionPtr revIDLastSave=\"0\" documentId=\"13_ncr:1_{0E1129D9-B2E4-4F74-BDFA-CE27885248CE}\" xr6:coauthVersionLast=\"45\" xr6:coauthVersionMax=\"45\" xr10:uidLastSave=\"{00000000-0000-0000-0000-000000000000}\" xmlns:xr10=\"http://schemas.microsoft.com/office/spreadsheetml/2016/revision10\" xmlns:xr6=\"http://schemas.microsoft.com/office/spreadsheetml/2016/revision6\" xmlns:xr=\"http://schemas.microsoft.com/office/spreadsheetml/2014/revision\" />");

            BookViews bookViews1 = new BookViews();

            WorkbookView workbookView1 = new WorkbookView() { XWindow = -28920, YWindow = -120, WindowWidth = (UInt32Value)29040U, WindowHeight = (UInt32Value)15840U, FirstSheet = (UInt32Value)1U, ActiveTab = (UInt32Value)1U };
            //workbookView1.SetAttribute(new OpenXmlAttribute("xr2", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2", "{00000000-000D-0000-FFFF-FFFF00000000}"));

            bookViews1.Append(workbookView1);

            Sheets sheets1 = new Sheets();
            Sheet sheet1 = new Sheet() { Name = "focus_planner_config", SheetId = (UInt32Value)3U, State = SheetStateValues.Hidden, Id = "rId1" };
            //Sheet sheet2 = new Sheet() { Name = "Sheet2", SheetId = (UInt32Value)8U, Id = "rId2" };
            Sheet sheet3 = new Sheet() { Name = "Cover", SheetId = (UInt32Value)10U, Id = "rId3" };
            Sheet sheet4 = new Sheet() { Name = "Focus Task Data", SheetId = (UInt32Value)9U, Id = "rId4" };

            sheets1.Append(sheet1);
            //sheets1.Append(sheet2);
            sheets1.Append(sheet3);
            sheets1.Append(sheet4);
            CalculationProperties calculationProperties1 = new CalculationProperties() { CalculationId = (UInt32Value)191028U };

            PivotCaches pivotCaches1 = new PivotCaches();
            PivotCache pivotCache1 = new PivotCache() { CacheId = (UInt32Value)4U, Id = "rId5" };

            pivotCaches1.Append(pivotCache1);

            WorkbookExtensionList workbookExtensionList1 = new WorkbookExtensionList();

            WorkbookExtension workbookExtension1 = new WorkbookExtension() { Uri = "{B58B0392-4F1F-4190-BB64-5DF3571DCE5F}" };
            workbookExtension1.AddNamespaceDeclaration("xcalcf", "http://schemas.microsoft.com/office/spreadsheetml/2018/calcfeatures");

            OpenXmlUnknownElement openXmlUnknownElement3 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<xcalcf:calcFeatures xmlns:xcalcf=\"http://schemas.microsoft.com/office/spreadsheetml/2018/calcfeatures\"><xcalcf:feature name=\"microsoft.com:RD\" /><xcalcf:feature name=\"microsoft.com:FV\" /></xcalcf:calcFeatures>");

            workbookExtension1.Append(openXmlUnknownElement3);

            workbookExtensionList1.Append(workbookExtension1);

            workbook1.Append(fileVersion1);
            workbook1.Append(workbookProperties1);
            workbook1.Append(alternateContent1);
            //workbook1.Append(openXmlUnknownElement2);
            workbook1.Append(bookViews1);
            workbook1.Append(sheets1);
            workbook1.Append(calculationProperties1);
            workbook1.Append(pivotCaches1);
            workbook1.Append(workbookExtensionList1);

            workbookPart1.Workbook = workbook1;
        }

        // Generates content of sharedStringTablePart1.
        private void GenerateSharedStringTablePart1Content(SharedStringTablePart sharedStringTablePart1)
        {
            SharedStringTable sharedStringTable1 = new SharedStringTable() { Count = (UInt32Value)88U, UniqueCount = (UInt32Value)54U };

            SharedStringItem sharedStringItem1 = new SharedStringItem();
            Text text1 = new Text();
            text1.Text = "Bucket";

            sharedStringItem1.Append(text1);

            SharedStringItem sharedStringItem2 = new SharedStringItem();
            Text text2 = new Text();
            text2.Text = "ChemCo Distilling Visit";

            sharedStringItem2.Append(text2);

            SharedStringItem sharedStringItem3 = new SharedStringItem();
            Text text3 = new Text();
            text3.Text = "Focus Bucket";

            sharedStringItem3.Append(text3);

            SharedStringItem sharedStringItem4 = new SharedStringItem();
            Text text4 = new Text();
            text4.Text = "Redmond Site Visit";

            sharedStringItem4.Append(text4);

            SharedStringItem sharedStringItem5 = new SharedStringItem();
            Text text5 = new Text();
            text5.Text = "planid";

            sharedStringItem5.Append(text5);

            SharedStringItem sharedStringItem6 = new SharedStringItem();
            Text text6 = new Text();
            text6.Text = "0wnpDRazhkmmFqbDTGyrj2UACGYj";

            sharedStringItem6.Append(text6);

            SharedStringItem sharedStringItem7 = new SharedStringItem();
            Text text7 = new Text();
            text7.Text = "bucketid";

            sharedStringItem7.Append(text7);

            SharedStringItem sharedStringItem8 = new SharedStringItem();
            Text text8 = new Text();
            text8.Text = "bN2qlVl-f0Kxo8IzpYxZp2UAMvGr";

            sharedStringItem8.Append(text8);

            SharedStringItem sharedStringItem9 = new SharedStringItem();
            Text text9 = new Text();
            text9.Text = "Task";

            sharedStringItem9.Append(text9);

            SharedStringItem sharedStringItem10 = new SharedStringItem();
            Text text10 = new Text();
            text10.Text = "Owner";

            sharedStringItem10.Append(text10);

            SharedStringItem sharedStringItem11 = new SharedStringItem();
            Text text11 = new Text();
            text11.Text = "Email";

            sharedStringItem11.Append(text11);

            SharedStringItem sharedStringItem12 = new SharedStringItem();
            Text text12 = new Text();
            text12.Text = "Progress";

            sharedStringItem12.Append(text12);

            SharedStringItem sharedStringItem13 = new SharedStringItem();
            Text text13 = new Text();
            text13.Text = "Due Date";

            sharedStringItem13.Append(text13);

            SharedStringItem sharedStringItem14 = new SharedStringItem();
            Text text14 = new Text();
            text14.Text = "Completed Date";

            sharedStringItem14.Append(text14);

            SharedStringItem sharedStringItem15 = new SharedStringItem();
            Text text15 = new Text();
            text15.Text = "Completed By";

            sharedStringItem15.Append(text15);

            SharedStringItem sharedStringItem16 = new SharedStringItem();
            Text text16 = new Text();
            text16.Text = "Created Date";

            sharedStringItem16.Append(text16);

            SharedStringItem sharedStringItem17 = new SharedStringItem();
            Text text17 = new Text();
            text17.Text = "Task Id";

            sharedStringItem17.Append(text17);

            SharedStringItem sharedStringItem18 = new SharedStringItem();
            Text text18 = new Text();
            text18.Text = "Temp control unit 3 fault";

            sharedStringItem18.Append(text18);

            SharedStringItem sharedStringItem19 = new SharedStringItem();
            Text text19 = new Text();
            text19.Text = "Will Gregg";

            sharedStringItem19.Append(text19);

            SharedStringItem sharedStringItem20 = new SharedStringItem();
            Text text20 = new Text();
            text20.Text = "grjoh@jebosoft.onmicrosoft.com";

            sharedStringItem20.Append(text20);

            SharedStringItem sharedStringItem21 = new SharedStringItem();
            Text text21 = new Text();
            text21.Text = "Not started";

            sharedStringItem21.Append(text21);

            SharedStringItem sharedStringItem22 = new SharedStringItem();
            Text text22 = new Text();
            text22.Text = "Tue Oct 15 2019";

            sharedStringItem22.Append(text22);

            SharedStringItem sharedStringItem23 = new SharedStringItem();
            Text text23 = new Text();
            text23.Text = "Tue Oct 08 2019";

            sharedStringItem23.Append(text23);

            SharedStringItem sharedStringItem24 = new SharedStringItem();
            Text text24 = new Text();
            text24.Text = "XsXC7qa2R0OiDwqEBjdXzGUACL9V";

            sharedStringItem24.Append(text24);

            SharedStringItem sharedStringItem25 = new SharedStringItem();
            Text text25 = new Text();
            text25.Text = "Cafe espresso machine leak";

            sharedStringItem25.Append(text25);

            SharedStringItem sharedStringItem26 = new SharedStringItem();
            Text text26 = new Text();
            text26.Text = "Tarun Chopra";

            sharedStringItem26.Append(text26);

            SharedStringItem sharedStringItem27 = new SharedStringItem();
            Text text27 = new Text();
            text27.Text = "tarunc@jebosoft.onmicrosoft.com";

            sharedStringItem27.Append(text27);

            SharedStringItem sharedStringItem28 = new SharedStringItem();
            Text text28 = new Text();
            text28.Text = "In Progress";

            sharedStringItem28.Append(text28);

            SharedStringItem sharedStringItem29 = new SharedStringItem();
            Text text29 = new Text();
            text29.Text = "Mon Oct 28 2019";

            sharedStringItem29.Append(text29);

            SharedStringItem sharedStringItem30 = new SharedStringItem();
            Text text30 = new Text();
            text30.Text = "Mon Oct 07 2019";

            sharedStringItem30.Append(text30);

            SharedStringItem sharedStringItem31 = new SharedStringItem();
            Text text31 = new Text();
            text31.Text = "m_KW-UFMKU-DPR0BmZZbW2UAKkxu";

            sharedStringItem31.Append(text31);

            SharedStringItem sharedStringItem32 = new SharedStringItem();
            Text text32 = new Text();
            text32.Text = "Painting broken";

            sharedStringItem32.Append(text32);

            SharedStringItem sharedStringItem33 = new SharedStringItem();
            Text text33 = new Text();
            text33.Text = "Tom Jebo";

            sharedStringItem33.Append(text33);

            SharedStringItem sharedStringItem34 = new SharedStringItem();
            Text text34 = new Text();
            text34.Text = "tomjebo@jebosoft.onmicrosoft.com";

            sharedStringItem34.Append(text34);

            SharedStringItem sharedStringItem35 = new SharedStringItem();
            Text text35 = new Text();
            text35.Text = "Sun Oct 06 2019";

            sharedStringItem35.Append(text35);

            SharedStringItem sharedStringItem36 = new SharedStringItem();
            Text text36 = new Text();
            text36.Text = "KpZ1JQ7pBUK9_Vc5vkNaOGUALtPI";

            sharedStringItem36.Append(text36);

            SharedStringItem sharedStringItem37 = new SharedStringItem();
            Text text37 = new Text();
            text37.Text = "Painting problem";

            sharedStringItem37.Append(text37);

            SharedStringItem sharedStringItem38 = new SharedStringItem();
            Text text38 = new Text();
            text38.Text = "Wed Oct 09 2019";

            sharedStringItem38.Append(text38);

            SharedStringItem sharedStringItem39 = new SharedStringItem();
            Text text39 = new Text();
            text39.Text = "Sat Oct 05 2019";

            sharedStringItem39.Append(text39);

            SharedStringItem sharedStringItem40 = new SharedStringItem();
            Text text40 = new Text();
            text40.Text = "AwFR1Ni2mEynSonFD0-3G2UAOfd2";

            sharedStringItem40.Append(text40);

            SharedStringItem sharedStringItem41 = new SharedStringItem();
            Text text41 = new Text();
            text41.Text = "New issue";

            sharedStringItem41.Append(text41);

            SharedStringItem sharedStringItem42 = new SharedStringItem();
            Text text42 = new Text();
            text42.Text = "Fri Oct 11 2019";

            sharedStringItem42.Append(text42);

            SharedStringItem sharedStringItem43 = new SharedStringItem();
            Text text43 = new Text();
            text43.Text = "Fri Oct 04 2019";

            sharedStringItem43.Append(text43);

            SharedStringItem sharedStringItem44 = new SharedStringItem();
            Text text44 = new Text();
            text44.Text = "6W6L9MEr6kmCZtYIcYsb2mUAJfOu";

            sharedStringItem44.Append(text44);

            SharedStringItem sharedStringItem45 = new SharedStringItem();
            Text text45 = new Text();
            text45.Text = "Jean Reno is cool";

            sharedStringItem45.Append(text45);

            SharedStringItem sharedStringItem46 = new SharedStringItem();
            Text text46 = new Text();
            text46.Text = "Thu Oct 03 2019";

            sharedStringItem46.Append(text46);

            SharedStringItem sharedStringItem47 = new SharedStringItem();
            Text text47 = new Text();
            text47.Text = "rabpSxRAbES1Mp6Ysl1c4WUAIxzx";

            sharedStringItem47.Append(text47);

            SharedStringItem sharedStringItem48 = new SharedStringItem();
            Text text48 = new Text();
            text48.Text = "McDonald\'s deep frier repair";

            sharedStringItem48.Append(text48);

            SharedStringItem sharedStringItem49 = new SharedStringItem();
            Text text49 = new Text();
            text49.Text = "Completed";

            sharedStringItem49.Append(text49);

            SharedStringItem sharedStringItem50 = new SharedStringItem();
            Text text50 = new Text();
            text50.Text = "Row Labels";

            sharedStringItem50.Append(text50);

            SharedStringItem sharedStringItem51 = new SharedStringItem();
            Text text51 = new Text();
            text51.Text = "Count of Task";

            sharedStringItem51.Append(text51);

            SharedStringItem sharedStringItem52 = new SharedStringItem();
            Text text52 = new Text();
            text52.Text = "Grand Total";

            sharedStringItem52.Append(text52);

            SharedStringItem sharedStringItem53 = new SharedStringItem();
            Text text53 = new Text();
            text53.Text = "Column Labels";

            sharedStringItem53.Append(text53);

            SharedStringItem sharedStringItem54 = new SharedStringItem();
            Text text54 = new Text();
            text54.Text = "Test Inline Str";

            sharedStringItem54.Append(text54);

            sharedStringTable1.Append(sharedStringItem1);
            sharedStringTable1.Append(sharedStringItem2);
            sharedStringTable1.Append(sharedStringItem3);
            sharedStringTable1.Append(sharedStringItem4);
            sharedStringTable1.Append(sharedStringItem5);
            sharedStringTable1.Append(sharedStringItem6);
            sharedStringTable1.Append(sharedStringItem7);
            sharedStringTable1.Append(sharedStringItem8);
            sharedStringTable1.Append(sharedStringItem9);
            sharedStringTable1.Append(sharedStringItem10);
            sharedStringTable1.Append(sharedStringItem11);
            sharedStringTable1.Append(sharedStringItem12);
            sharedStringTable1.Append(sharedStringItem13);
            sharedStringTable1.Append(sharedStringItem14);
            sharedStringTable1.Append(sharedStringItem15);
            sharedStringTable1.Append(sharedStringItem16);
            sharedStringTable1.Append(sharedStringItem17);
            sharedStringTable1.Append(sharedStringItem18);
            sharedStringTable1.Append(sharedStringItem19);
            sharedStringTable1.Append(sharedStringItem20);
            sharedStringTable1.Append(sharedStringItem21);
            sharedStringTable1.Append(sharedStringItem22);
            sharedStringTable1.Append(sharedStringItem23);
            sharedStringTable1.Append(sharedStringItem24);
            sharedStringTable1.Append(sharedStringItem25);
            sharedStringTable1.Append(sharedStringItem26);
            sharedStringTable1.Append(sharedStringItem27);
            sharedStringTable1.Append(sharedStringItem28);
            sharedStringTable1.Append(sharedStringItem29);
            sharedStringTable1.Append(sharedStringItem30);
            sharedStringTable1.Append(sharedStringItem31);
            sharedStringTable1.Append(sharedStringItem32);
            sharedStringTable1.Append(sharedStringItem33);
            sharedStringTable1.Append(sharedStringItem34);
            sharedStringTable1.Append(sharedStringItem35);
            sharedStringTable1.Append(sharedStringItem36);
            sharedStringTable1.Append(sharedStringItem37);
            sharedStringTable1.Append(sharedStringItem38);
            sharedStringTable1.Append(sharedStringItem39);
            sharedStringTable1.Append(sharedStringItem40);
            sharedStringTable1.Append(sharedStringItem41);
            sharedStringTable1.Append(sharedStringItem42);
            sharedStringTable1.Append(sharedStringItem43);
            sharedStringTable1.Append(sharedStringItem44);
            sharedStringTable1.Append(sharedStringItem45);
            sharedStringTable1.Append(sharedStringItem46);
            sharedStringTable1.Append(sharedStringItem47);
            sharedStringTable1.Append(sharedStringItem48);
            sharedStringTable1.Append(sharedStringItem49);
            sharedStringTable1.Append(sharedStringItem50);
            sharedStringTable1.Append(sharedStringItem51);
            sharedStringTable1.Append(sharedStringItem52);
            sharedStringTable1.Append(sharedStringItem53);
            sharedStringTable1.Append(sharedStringItem54);

            sharedStringTablePart1.SharedStringTable = sharedStringTable1;
        }

        // Generates content of worksheetPart1.
        private void GenerateWorksheetPart1Content(WorksheetPart worksheetPart1)
        {
            Worksheet worksheet1 = new Worksheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac" } };
            worksheet1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            worksheet1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            worksheet1.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
            //worksheet1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //worksheet1.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
            //worksheet1.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //worksheet1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{1C57A41C-8A0C-4805-BA26-93832100FC89}"));
            SheetDimension sheetDimension1 = new SheetDimension() { Reference = "A1:M38" };

            SheetViews sheetViews1 = new SheetViews();

            SheetView sheetView1 = new SheetView() { TabSelected = true, WorkbookViewId = (UInt32Value)0U };
            Selection selection1 = new Selection() { ActiveCell = "P33", SequenceOfReferences = new ListValue<StringValue>() { InnerText = "P33" } };

            sheetView1.Append(selection1);

            sheetViews1.Append(sheetView1);
            SheetFormatProperties sheetFormatProperties1 = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

            Columns columns1 = new Columns();
            Column column1 = new Column() { Min = (UInt32Value)1U, Max = (UInt32Value)1U, Width = 21.5703125D, BestFit = true, CustomWidth = true };
            Column column2 = new Column() { Min = (UInt32Value)2U, Max = (UInt32Value)2U, Width = 16.28515625D, BestFit = true, CustomWidth = true };
            Column column3 = new Column() { Min = (UInt32Value)3U, Max = (UInt32Value)3U, Width = 10.7109375D, BestFit = true, CustomWidth = true };
            Column column4 = new Column() { Min = (UInt32Value)4U, Max = (UInt32Value)4U, Width = 11D, BestFit = true, CustomWidth = true };
            Column column5 = new Column() { Min = (UInt32Value)5U, Max = (UInt32Value)5U, Width = 11.28515625D, BestFit = true, CustomWidth = true };
            Column column6 = new Column() { Min = (UInt32Value)8U, Max = (UInt32Value)8U, Width = 13.140625D, BestFit = true, CustomWidth = true };
            Column column7 = new Column() { Min = (UInt32Value)9U, Max = (UInt32Value)9U, Width = 12.85546875D, BestFit = true, CustomWidth = true };
            Column column8 = new Column() { Min = (UInt32Value)10U, Max = (UInt32Value)10U, Width = 16.140625D, BestFit = true, CustomWidth = true };
            Column column9 = new Column() { Min = (UInt32Value)11U, Max = (UInt32Value)11U, Width = 10.140625D, BestFit = true, CustomWidth = true };
            Column column10 = new Column() { Min = (UInt32Value)12U, Max = (UInt32Value)12U, Width = 15.140625D, BestFit = true, CustomWidth = true };
            Column column11 = new Column() { Min = (UInt32Value)13U, Max = (UInt32Value)13U, Width = 16.42578125D, BestFit = true, CustomWidth = true };
            Column column12 = new Column() { Min = (UInt32Value)14U, Max = (UInt32Value)14U, Width = 23.140625D, BestFit = true, CustomWidth = true };
            Column column13 = new Column() { Min = (UInt32Value)15U, Max = (UInt32Value)15U, Width = 11.28515625D, BestFit = true, CustomWidth = true };

            columns1.Append(column1);
            columns1.Append(column2);
            columns1.Append(column3);
            columns1.Append(column4);
            columns1.Append(column5);
            columns1.Append(column6);
            columns1.Append(column7);
            columns1.Append(column8);
            columns1.Append(column9);
            columns1.Append(column10);
            columns1.Append(column11);
            columns1.Append(column12);
            columns1.Append(column13);

            SheetData sheetData1 = new SheetData();

            Row row1 = new Row() { RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell1 = new Cell() { CellReference = "A1", StyleIndex = (UInt32Value)4U };
            Cell cell2 = new Cell() { CellReference = "B1", StyleIndex = (UInt32Value)5U };
            Cell cell3 = new Cell() { CellReference = "C1", StyleIndex = (UInt32Value)5U };
            Cell cell4 = new Cell() { CellReference = "D1", StyleIndex = (UInt32Value)5U };
            Cell cell5 = new Cell() { CellReference = "E1", StyleIndex = (UInt32Value)5U };
            Cell cell6 = new Cell() { CellReference = "F1", StyleIndex = (UInt32Value)5U };
            Cell cell7 = new Cell() { CellReference = "G1", StyleIndex = (UInt32Value)5U };
            Cell cell8 = new Cell() { CellReference = "H1", StyleIndex = (UInt32Value)5U };
            Cell cell9 = new Cell() { CellReference = "I1", StyleIndex = (UInt32Value)5U };
            Cell cell10 = new Cell() { CellReference = "J1", StyleIndex = (UInt32Value)5U };
            Cell cell11 = new Cell() { CellReference = "K1", StyleIndex = (UInt32Value)5U };
            Cell cell12 = new Cell() { CellReference = "L1", StyleIndex = (UInt32Value)5U };
            Cell cell13 = new Cell() { CellReference = "M1", StyleIndex = (UInt32Value)6U };

            row1.Append(cell1);
            row1.Append(cell2);
            row1.Append(cell3);
            row1.Append(cell4);
            row1.Append(cell5);
            row1.Append(cell6);
            row1.Append(cell7);
            row1.Append(cell8);
            row1.Append(cell9);
            row1.Append(cell10);
            row1.Append(cell11);
            row1.Append(cell12);
            row1.Append(cell13);

            Row row2 = new Row() { RowIndex = (UInt32Value)2U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell14 = new Cell() { CellReference = "A2", StyleIndex = (UInt32Value)7U };
            Cell cell15 = new Cell() { CellReference = "B2", StyleIndex = (UInt32Value)8U };
            Cell cell16 = new Cell() { CellReference = "C2", StyleIndex = (UInt32Value)8U };
            Cell cell17 = new Cell() { CellReference = "D2", StyleIndex = (UInt32Value)8U };
            Cell cell18 = new Cell() { CellReference = "E2", StyleIndex = (UInt32Value)8U };
            Cell cell19 = new Cell() { CellReference = "F2", StyleIndex = (UInt32Value)8U };
            Cell cell20 = new Cell() { CellReference = "G2", StyleIndex = (UInt32Value)8U };
            Cell cell21 = new Cell() { CellReference = "H2", StyleIndex = (UInt32Value)8U };
            Cell cell22 = new Cell() { CellReference = "I2", StyleIndex = (UInt32Value)8U };
            Cell cell23 = new Cell() { CellReference = "J2", StyleIndex = (UInt32Value)8U };
            Cell cell24 = new Cell() { CellReference = "K2", StyleIndex = (UInt32Value)8U };
            Cell cell25 = new Cell() { CellReference = "L2", StyleIndex = (UInt32Value)8U };
            Cell cell26 = new Cell() { CellReference = "M2", StyleIndex = (UInt32Value)9U };

            row2.Append(cell14);
            row2.Append(cell15);
            row2.Append(cell16);
            row2.Append(cell17);
            row2.Append(cell18);
            row2.Append(cell19);
            row2.Append(cell20);
            row2.Append(cell21);
            row2.Append(cell22);
            row2.Append(cell23);
            row2.Append(cell24);
            row2.Append(cell25);
            row2.Append(cell26);

            Row row3 = new Row() { RowIndex = (UInt32Value)3U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell27 = new Cell() { CellReference = "A3", StyleIndex = (UInt32Value)7U };
            Cell cell28 = new Cell() { CellReference = "B3", StyleIndex = (UInt32Value)8U };
            Cell cell29 = new Cell() { CellReference = "C3", StyleIndex = (UInt32Value)8U };
            Cell cell30 = new Cell() { CellReference = "D3", StyleIndex = (UInt32Value)8U };
            Cell cell31 = new Cell() { CellReference = "E3", StyleIndex = (UInt32Value)8U };
            Cell cell32 = new Cell() { CellReference = "F3", StyleIndex = (UInt32Value)8U };
            Cell cell33 = new Cell() { CellReference = "G3", StyleIndex = (UInt32Value)8U };
            Cell cell34 = new Cell() { CellReference = "H3", StyleIndex = (UInt32Value)8U };
            Cell cell35 = new Cell() { CellReference = "I3", StyleIndex = (UInt32Value)8U };
            Cell cell36 = new Cell() { CellReference = "J3", StyleIndex = (UInt32Value)8U };
            Cell cell37 = new Cell() { CellReference = "K3", StyleIndex = (UInt32Value)8U };
            Cell cell38 = new Cell() { CellReference = "L3", StyleIndex = (UInt32Value)8U };
            Cell cell39 = new Cell() { CellReference = "M3", StyleIndex = (UInt32Value)9U };

            row3.Append(cell27);
            row3.Append(cell28);
            row3.Append(cell29);
            row3.Append(cell30);
            row3.Append(cell31);
            row3.Append(cell32);
            row3.Append(cell33);
            row3.Append(cell34);
            row3.Append(cell35);
            row3.Append(cell36);
            row3.Append(cell37);
            row3.Append(cell38);
            row3.Append(cell39);

            Row row4 = new Row() { RowIndex = (UInt32Value)4U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell40 = new Cell() { CellReference = "A4", StyleIndex = (UInt32Value)7U };
            Cell cell41 = new Cell() { CellReference = "B4", StyleIndex = (UInt32Value)8U };
            Cell cell42 = new Cell() { CellReference = "C4", StyleIndex = (UInt32Value)8U };
            Cell cell43 = new Cell() { CellReference = "D4", StyleIndex = (UInt32Value)8U };
            Cell cell44 = new Cell() { CellReference = "E4", StyleIndex = (UInt32Value)8U };
            Cell cell45 = new Cell() { CellReference = "F4", StyleIndex = (UInt32Value)8U };
            Cell cell46 = new Cell() { CellReference = "G4", StyleIndex = (UInt32Value)8U };
            Cell cell47 = new Cell() { CellReference = "H4", StyleIndex = (UInt32Value)8U };
            Cell cell48 = new Cell() { CellReference = "I4", StyleIndex = (UInt32Value)8U };
            Cell cell49 = new Cell() { CellReference = "J4", StyleIndex = (UInt32Value)8U };
            Cell cell50 = new Cell() { CellReference = "K4", StyleIndex = (UInt32Value)8U };
            Cell cell51 = new Cell() { CellReference = "L4", StyleIndex = (UInt32Value)8U };
            Cell cell52 = new Cell() { CellReference = "M4", StyleIndex = (UInt32Value)9U };

            row4.Append(cell40);
            row4.Append(cell41);
            row4.Append(cell42);
            row4.Append(cell43);
            row4.Append(cell44);
            row4.Append(cell45);
            row4.Append(cell46);
            row4.Append(cell47);
            row4.Append(cell48);
            row4.Append(cell49);
            row4.Append(cell50);
            row4.Append(cell51);
            row4.Append(cell52);

            Row row5 = new Row() { RowIndex = (UInt32Value)5U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell53 = new Cell() { CellReference = "A5", StyleIndex = (UInt32Value)7U };
            Cell cell54 = new Cell() { CellReference = "B5", StyleIndex = (UInt32Value)8U };
            Cell cell55 = new Cell() { CellReference = "C5", StyleIndex = (UInt32Value)8U };
            Cell cell56 = new Cell() { CellReference = "D5", StyleIndex = (UInt32Value)8U };
            Cell cell57 = new Cell() { CellReference = "E5", StyleIndex = (UInt32Value)8U };
            Cell cell58 = new Cell() { CellReference = "F5", StyleIndex = (UInt32Value)8U };
            Cell cell59 = new Cell() { CellReference = "G5", StyleIndex = (UInt32Value)8U };
            Cell cell60 = new Cell() { CellReference = "H5", StyleIndex = (UInt32Value)8U };
            Cell cell61 = new Cell() { CellReference = "I5", StyleIndex = (UInt32Value)8U };
            Cell cell62 = new Cell() { CellReference = "J5", StyleIndex = (UInt32Value)8U };
            Cell cell63 = new Cell() { CellReference = "K5", StyleIndex = (UInt32Value)8U };
            Cell cell64 = new Cell() { CellReference = "L5", StyleIndex = (UInt32Value)8U };
            Cell cell65 = new Cell() { CellReference = "M5", StyleIndex = (UInt32Value)9U };

            row5.Append(cell53);
            row5.Append(cell54);
            row5.Append(cell55);
            row5.Append(cell56);
            row5.Append(cell57);
            row5.Append(cell58);
            row5.Append(cell59);
            row5.Append(cell60);
            row5.Append(cell61);
            row5.Append(cell62);
            row5.Append(cell63);
            row5.Append(cell64);
            row5.Append(cell65);

            Row row6 = new Row() { RowIndex = (UInt32Value)6U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell66 = new Cell() { CellReference = "A6", StyleIndex = (UInt32Value)7U };
            Cell cell67 = new Cell() { CellReference = "B6", StyleIndex = (UInt32Value)8U };
            Cell cell68 = new Cell() { CellReference = "C6", StyleIndex = (UInt32Value)8U };
            Cell cell69 = new Cell() { CellReference = "D6", StyleIndex = (UInt32Value)8U };
            Cell cell70 = new Cell() { CellReference = "E6", StyleIndex = (UInt32Value)8U };
            Cell cell71 = new Cell() { CellReference = "F6", StyleIndex = (UInt32Value)8U };
            Cell cell72 = new Cell() { CellReference = "G6", StyleIndex = (UInt32Value)8U };
            Cell cell73 = new Cell() { CellReference = "H6", StyleIndex = (UInt32Value)8U };
            Cell cell74 = new Cell() { CellReference = "I6", StyleIndex = (UInt32Value)8U };
            Cell cell75 = new Cell() { CellReference = "J6", StyleIndex = (UInt32Value)8U };
            Cell cell76 = new Cell() { CellReference = "K6", StyleIndex = (UInt32Value)8U };
            Cell cell77 = new Cell() { CellReference = "L6", StyleIndex = (UInt32Value)8U };
            Cell cell78 = new Cell() { CellReference = "M6", StyleIndex = (UInt32Value)9U };

            row6.Append(cell66);
            row6.Append(cell67);
            row6.Append(cell68);
            row6.Append(cell69);
            row6.Append(cell70);
            row6.Append(cell71);
            row6.Append(cell72);
            row6.Append(cell73);
            row6.Append(cell74);
            row6.Append(cell75);
            row6.Append(cell76);
            row6.Append(cell77);
            row6.Append(cell78);

            Row row7 = new Row() { RowIndex = (UInt32Value)7U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell79 = new Cell() { CellReference = "A7", StyleIndex = (UInt32Value)7U };
            Cell cell80 = new Cell() { CellReference = "B7", StyleIndex = (UInt32Value)8U };
            Cell cell81 = new Cell() { CellReference = "C7", StyleIndex = (UInt32Value)8U };
            Cell cell82 = new Cell() { CellReference = "D7", StyleIndex = (UInt32Value)8U };
            Cell cell83 = new Cell() { CellReference = "E7", StyleIndex = (UInt32Value)8U };
            Cell cell84 = new Cell() { CellReference = "F7", StyleIndex = (UInt32Value)8U };
            Cell cell85 = new Cell() { CellReference = "G7", StyleIndex = (UInt32Value)8U };
            Cell cell86 = new Cell() { CellReference = "H7", StyleIndex = (UInt32Value)8U };
            Cell cell87 = new Cell() { CellReference = "I7", StyleIndex = (UInt32Value)8U };
            Cell cell88 = new Cell() { CellReference = "J7", StyleIndex = (UInt32Value)8U };
            Cell cell89 = new Cell() { CellReference = "K7", StyleIndex = (UInt32Value)8U };
            Cell cell90 = new Cell() { CellReference = "L7", StyleIndex = (UInt32Value)8U };
            Cell cell91 = new Cell() { CellReference = "M7", StyleIndex = (UInt32Value)9U };

            row7.Append(cell79);
            row7.Append(cell80);
            row7.Append(cell81);
            row7.Append(cell82);
            row7.Append(cell83);
            row7.Append(cell84);
            row7.Append(cell85);
            row7.Append(cell86);
            row7.Append(cell87);
            row7.Append(cell88);
            row7.Append(cell89);
            row7.Append(cell90);
            row7.Append(cell91);

            Row row8 = new Row() { RowIndex = (UInt32Value)8U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell92 = new Cell() { CellReference = "A8", StyleIndex = (UInt32Value)7U };
            Cell cell93 = new Cell() { CellReference = "B8", StyleIndex = (UInt32Value)8U };
            Cell cell94 = new Cell() { CellReference = "C8", StyleIndex = (UInt32Value)8U };
            Cell cell95 = new Cell() { CellReference = "D8", StyleIndex = (UInt32Value)8U };
            Cell cell96 = new Cell() { CellReference = "E8", StyleIndex = (UInt32Value)8U };
            Cell cell97 = new Cell() { CellReference = "F8", StyleIndex = (UInt32Value)8U };
            Cell cell98 = new Cell() { CellReference = "G8", StyleIndex = (UInt32Value)8U };
            Cell cell99 = new Cell() { CellReference = "H8", StyleIndex = (UInt32Value)8U };
            Cell cell100 = new Cell() { CellReference = "I8", StyleIndex = (UInt32Value)8U };
            Cell cell101 = new Cell() { CellReference = "J8", StyleIndex = (UInt32Value)8U };
            Cell cell102 = new Cell() { CellReference = "K8", StyleIndex = (UInt32Value)8U };
            Cell cell103 = new Cell() { CellReference = "L8", StyleIndex = (UInt32Value)8U };
            Cell cell104 = new Cell() { CellReference = "M8", StyleIndex = (UInt32Value)9U };

            row8.Append(cell92);
            row8.Append(cell93);
            row8.Append(cell94);
            row8.Append(cell95);
            row8.Append(cell96);
            row8.Append(cell97);
            row8.Append(cell98);
            row8.Append(cell99);
            row8.Append(cell100);
            row8.Append(cell101);
            row8.Append(cell102);
            row8.Append(cell103);
            row8.Append(cell104);

            Row row9 = new Row() { RowIndex = (UInt32Value)9U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell105 = new Cell() { CellReference = "A9", StyleIndex = (UInt32Value)7U };
            Cell cell106 = new Cell() { CellReference = "B9", StyleIndex = (UInt32Value)8U };
            Cell cell107 = new Cell() { CellReference = "C9", StyleIndex = (UInt32Value)8U };
            Cell cell108 = new Cell() { CellReference = "D9", StyleIndex = (UInt32Value)8U };
            Cell cell109 = new Cell() { CellReference = "E9", StyleIndex = (UInt32Value)8U };
            Cell cell110 = new Cell() { CellReference = "F9", StyleIndex = (UInt32Value)8U };
            Cell cell111 = new Cell() { CellReference = "G9", StyleIndex = (UInt32Value)8U };
            Cell cell112 = new Cell() { CellReference = "H9", StyleIndex = (UInt32Value)8U };
            Cell cell113 = new Cell() { CellReference = "I9", StyleIndex = (UInt32Value)8U };
            Cell cell114 = new Cell() { CellReference = "J9", StyleIndex = (UInt32Value)8U };
            Cell cell115 = new Cell() { CellReference = "K9", StyleIndex = (UInt32Value)8U };
            Cell cell116 = new Cell() { CellReference = "L9", StyleIndex = (UInt32Value)8U };
            Cell cell117 = new Cell() { CellReference = "M9", StyleIndex = (UInt32Value)9U };

            row9.Append(cell105);
            row9.Append(cell106);
            row9.Append(cell107);
            row9.Append(cell108);
            row9.Append(cell109);
            row9.Append(cell110);
            row9.Append(cell111);
            row9.Append(cell112);
            row9.Append(cell113);
            row9.Append(cell114);
            row9.Append(cell115);
            row9.Append(cell116);
            row9.Append(cell117);

            Row row10 = new Row() { RowIndex = (UInt32Value)10U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell118 = new Cell() { CellReference = "A10", StyleIndex = (UInt32Value)7U };
            Cell cell119 = new Cell() { CellReference = "B10", StyleIndex = (UInt32Value)8U };
            Cell cell120 = new Cell() { CellReference = "C10", StyleIndex = (UInt32Value)8U };
            Cell cell121 = new Cell() { CellReference = "D10", StyleIndex = (UInt32Value)8U };
            Cell cell122 = new Cell() { CellReference = "E10", StyleIndex = (UInt32Value)8U };
            Cell cell123 = new Cell() { CellReference = "F10", StyleIndex = (UInt32Value)8U };
            Cell cell124 = new Cell() { CellReference = "G10", StyleIndex = (UInt32Value)8U };
            Cell cell125 = new Cell() { CellReference = "H10", StyleIndex = (UInt32Value)8U };
            Cell cell126 = new Cell() { CellReference = "I10", StyleIndex = (UInt32Value)8U };
            Cell cell127 = new Cell() { CellReference = "J10", StyleIndex = (UInt32Value)8U };
            Cell cell128 = new Cell() { CellReference = "K10", StyleIndex = (UInt32Value)8U };
            Cell cell129 = new Cell() { CellReference = "L10", StyleIndex = (UInt32Value)8U };
            Cell cell130 = new Cell() { CellReference = "M10", StyleIndex = (UInt32Value)9U };

            row10.Append(cell118);
            row10.Append(cell119);
            row10.Append(cell120);
            row10.Append(cell121);
            row10.Append(cell122);
            row10.Append(cell123);
            row10.Append(cell124);
            row10.Append(cell125);
            row10.Append(cell126);
            row10.Append(cell127);
            row10.Append(cell128);
            row10.Append(cell129);
            row10.Append(cell130);

            Row row11 = new Row() { RowIndex = (UInt32Value)11U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell131 = new Cell() { CellReference = "A11", StyleIndex = (UInt32Value)7U };
            Cell cell132 = new Cell() { CellReference = "B11", StyleIndex = (UInt32Value)8U };
            Cell cell133 = new Cell() { CellReference = "C11", StyleIndex = (UInt32Value)8U };
            Cell cell134 = new Cell() { CellReference = "D11", StyleIndex = (UInt32Value)8U };
            Cell cell135 = new Cell() { CellReference = "E11", StyleIndex = (UInt32Value)8U };
            Cell cell136 = new Cell() { CellReference = "F11", StyleIndex = (UInt32Value)8U };
            Cell cell137 = new Cell() { CellReference = "G11", StyleIndex = (UInt32Value)8U };
            Cell cell138 = new Cell() { CellReference = "H11", StyleIndex = (UInt32Value)8U };
            Cell cell139 = new Cell() { CellReference = "I11", StyleIndex = (UInt32Value)8U };
            Cell cell140 = new Cell() { CellReference = "J11", StyleIndex = (UInt32Value)8U };
            Cell cell141 = new Cell() { CellReference = "K11", StyleIndex = (UInt32Value)8U };
            Cell cell142 = new Cell() { CellReference = "L11", StyleIndex = (UInt32Value)8U };
            Cell cell143 = new Cell() { CellReference = "M11", StyleIndex = (UInt32Value)9U };

            row11.Append(cell131);
            row11.Append(cell132);
            row11.Append(cell133);
            row11.Append(cell134);
            row11.Append(cell135);
            row11.Append(cell136);
            row11.Append(cell137);
            row11.Append(cell138);
            row11.Append(cell139);
            row11.Append(cell140);
            row11.Append(cell141);
            row11.Append(cell142);
            row11.Append(cell143);

            Row row12 = new Row() { RowIndex = (UInt32Value)12U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell144 = new Cell() { CellReference = "A12", StyleIndex = (UInt32Value)7U };
            Cell cell145 = new Cell() { CellReference = "B12", StyleIndex = (UInt32Value)8U };
            Cell cell146 = new Cell() { CellReference = "C12", StyleIndex = (UInt32Value)8U };
            Cell cell147 = new Cell() { CellReference = "D12", StyleIndex = (UInt32Value)8U };
            Cell cell148 = new Cell() { CellReference = "E12", StyleIndex = (UInt32Value)8U };
            Cell cell149 = new Cell() { CellReference = "F12", StyleIndex = (UInt32Value)8U };
            Cell cell150 = new Cell() { CellReference = "G12", StyleIndex = (UInt32Value)8U };
            Cell cell151 = new Cell() { CellReference = "H12", StyleIndex = (UInt32Value)8U };
            Cell cell152 = new Cell() { CellReference = "I12", StyleIndex = (UInt32Value)8U };
            Cell cell153 = new Cell() { CellReference = "J12", StyleIndex = (UInt32Value)8U };
            Cell cell154 = new Cell() { CellReference = "K12", StyleIndex = (UInt32Value)8U };
            Cell cell155 = new Cell() { CellReference = "L12", StyleIndex = (UInt32Value)8U };
            Cell cell156 = new Cell() { CellReference = "M12", StyleIndex = (UInt32Value)9U };

            row12.Append(cell144);
            row12.Append(cell145);
            row12.Append(cell146);
            row12.Append(cell147);
            row12.Append(cell148);
            row12.Append(cell149);
            row12.Append(cell150);
            row12.Append(cell151);
            row12.Append(cell152);
            row12.Append(cell153);
            row12.Append(cell154);
            row12.Append(cell155);
            row12.Append(cell156);

            Row row13 = new Row() { RowIndex = (UInt32Value)13U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell157 = new Cell() { CellReference = "A13", StyleIndex = (UInt32Value)7U };
            Cell cell158 = new Cell() { CellReference = "B13", StyleIndex = (UInt32Value)8U };
            Cell cell159 = new Cell() { CellReference = "C13", StyleIndex = (UInt32Value)8U };
            Cell cell160 = new Cell() { CellReference = "D13", StyleIndex = (UInt32Value)8U };
            Cell cell161 = new Cell() { CellReference = "E13", StyleIndex = (UInt32Value)8U };
            Cell cell162 = new Cell() { CellReference = "F13", StyleIndex = (UInt32Value)8U };
            Cell cell163 = new Cell() { CellReference = "G13", StyleIndex = (UInt32Value)8U };
            Cell cell164 = new Cell() { CellReference = "H13", StyleIndex = (UInt32Value)8U };
            Cell cell165 = new Cell() { CellReference = "I13", StyleIndex = (UInt32Value)8U };
            Cell cell166 = new Cell() { CellReference = "J13", StyleIndex = (UInt32Value)8U };
            Cell cell167 = new Cell() { CellReference = "K13", StyleIndex = (UInt32Value)8U };
            Cell cell168 = new Cell() { CellReference = "L13", StyleIndex = (UInt32Value)8U };
            Cell cell169 = new Cell() { CellReference = "M13", StyleIndex = (UInt32Value)9U };

            row13.Append(cell157);
            row13.Append(cell158);
            row13.Append(cell159);
            row13.Append(cell160);
            row13.Append(cell161);
            row13.Append(cell162);
            row13.Append(cell163);
            row13.Append(cell164);
            row13.Append(cell165);
            row13.Append(cell166);
            row13.Append(cell167);
            row13.Append(cell168);
            row13.Append(cell169);

            Row row14 = new Row() { RowIndex = (UInt32Value)14U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell170 = new Cell() { CellReference = "A14", StyleIndex = (UInt32Value)7U };
            Cell cell171 = new Cell() { CellReference = "B14", StyleIndex = (UInt32Value)8U };
            Cell cell172 = new Cell() { CellReference = "C14", StyleIndex = (UInt32Value)8U };
            Cell cell173 = new Cell() { CellReference = "D14", StyleIndex = (UInt32Value)8U };
            Cell cell174 = new Cell() { CellReference = "E14", StyleIndex = (UInt32Value)8U };
            Cell cell175 = new Cell() { CellReference = "F14", StyleIndex = (UInt32Value)8U };
            Cell cell176 = new Cell() { CellReference = "G14", StyleIndex = (UInt32Value)8U };
            Cell cell177 = new Cell() { CellReference = "H14", StyleIndex = (UInt32Value)8U };
            Cell cell178 = new Cell() { CellReference = "I14", StyleIndex = (UInt32Value)8U };
            Cell cell179 = new Cell() { CellReference = "J14", StyleIndex = (UInt32Value)8U };
            Cell cell180 = new Cell() { CellReference = "K14", StyleIndex = (UInt32Value)8U };
            Cell cell181 = new Cell() { CellReference = "L14", StyleIndex = (UInt32Value)8U };
            Cell cell182 = new Cell() { CellReference = "M14", StyleIndex = (UInt32Value)9U };

            row14.Append(cell170);
            row14.Append(cell171);
            row14.Append(cell172);
            row14.Append(cell173);
            row14.Append(cell174);
            row14.Append(cell175);
            row14.Append(cell176);
            row14.Append(cell177);
            row14.Append(cell178);
            row14.Append(cell179);
            row14.Append(cell180);
            row14.Append(cell181);
            row14.Append(cell182);

            Row row15 = new Row() { RowIndex = (UInt32Value)15U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell183 = new Cell() { CellReference = "A15", StyleIndex = (UInt32Value)7U };
            Cell cell184 = new Cell() { CellReference = "B15", StyleIndex = (UInt32Value)8U };
            Cell cell185 = new Cell() { CellReference = "C15", StyleIndex = (UInt32Value)8U };
            Cell cell186 = new Cell() { CellReference = "D15", StyleIndex = (UInt32Value)8U };
            Cell cell187 = new Cell() { CellReference = "E15", StyleIndex = (UInt32Value)8U };
            Cell cell188 = new Cell() { CellReference = "F15", StyleIndex = (UInt32Value)8U };
            Cell cell189 = new Cell() { CellReference = "G15", StyleIndex = (UInt32Value)8U };
            Cell cell190 = new Cell() { CellReference = "H15", StyleIndex = (UInt32Value)8U };
            Cell cell191 = new Cell() { CellReference = "I15", StyleIndex = (UInt32Value)8U };
            Cell cell192 = new Cell() { CellReference = "J15", StyleIndex = (UInt32Value)8U };
            Cell cell193 = new Cell() { CellReference = "K15", StyleIndex = (UInt32Value)8U };
            Cell cell194 = new Cell() { CellReference = "L15", StyleIndex = (UInt32Value)8U };
            Cell cell195 = new Cell() { CellReference = "M15", StyleIndex = (UInt32Value)9U };

            row15.Append(cell183);
            row15.Append(cell184);
            row15.Append(cell185);
            row15.Append(cell186);
            row15.Append(cell187);
            row15.Append(cell188);
            row15.Append(cell189);
            row15.Append(cell190);
            row15.Append(cell191);
            row15.Append(cell192);
            row15.Append(cell193);
            row15.Append(cell194);
            row15.Append(cell195);

            Row row16 = new Row() { RowIndex = (UInt32Value)16U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell196 = new Cell() { CellReference = "A16", StyleIndex = (UInt32Value)7U };
            Cell cell197 = new Cell() { CellReference = "B16", StyleIndex = (UInt32Value)8U };
            Cell cell198 = new Cell() { CellReference = "C16", StyleIndex = (UInt32Value)8U };
            Cell cell199 = new Cell() { CellReference = "D16", StyleIndex = (UInt32Value)8U };
            Cell cell200 = new Cell() { CellReference = "E16", StyleIndex = (UInt32Value)8U };
            Cell cell201 = new Cell() { CellReference = "F16", StyleIndex = (UInt32Value)8U };
            Cell cell202 = new Cell() { CellReference = "G16", StyleIndex = (UInt32Value)8U };
            Cell cell203 = new Cell() { CellReference = "H16", StyleIndex = (UInt32Value)8U };
            Cell cell204 = new Cell() { CellReference = "I16", StyleIndex = (UInt32Value)8U };
            Cell cell205 = new Cell() { CellReference = "J16", StyleIndex = (UInt32Value)8U };
            Cell cell206 = new Cell() { CellReference = "K16", StyleIndex = (UInt32Value)8U };
            Cell cell207 = new Cell() { CellReference = "L16", StyleIndex = (UInt32Value)8U };
            Cell cell208 = new Cell() { CellReference = "M16", StyleIndex = (UInt32Value)9U };

            row16.Append(cell196);
            row16.Append(cell197);
            row16.Append(cell198);
            row16.Append(cell199);
            row16.Append(cell200);
            row16.Append(cell201);
            row16.Append(cell202);
            row16.Append(cell203);
            row16.Append(cell204);
            row16.Append(cell205);
            row16.Append(cell206);
            row16.Append(cell207);
            row16.Append(cell208);

            Row row17 = new Row() { RowIndex = (UInt32Value)17U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell209 = new Cell() { CellReference = "A17", StyleIndex = (UInt32Value)7U };
            Cell cell210 = new Cell() { CellReference = "B17", StyleIndex = (UInt32Value)8U };
            Cell cell211 = new Cell() { CellReference = "C17", StyleIndex = (UInt32Value)8U };
            Cell cell212 = new Cell() { CellReference = "D17", StyleIndex = (UInt32Value)8U };
            Cell cell213 = new Cell() { CellReference = "E17", StyleIndex = (UInt32Value)8U };
            Cell cell214 = new Cell() { CellReference = "F17", StyleIndex = (UInt32Value)8U };
            Cell cell215 = new Cell() { CellReference = "G17", StyleIndex = (UInt32Value)8U };
            Cell cell216 = new Cell() { CellReference = "H17", StyleIndex = (UInt32Value)8U };
            Cell cell217 = new Cell() { CellReference = "I17", StyleIndex = (UInt32Value)8U };
            Cell cell218 = new Cell() { CellReference = "J17", StyleIndex = (UInt32Value)8U };
            Cell cell219 = new Cell() { CellReference = "K17", StyleIndex = (UInt32Value)8U };
            Cell cell220 = new Cell() { CellReference = "L17", StyleIndex = (UInt32Value)8U };
            Cell cell221 = new Cell() { CellReference = "M17", StyleIndex = (UInt32Value)9U };

            row17.Append(cell209);
            row17.Append(cell210);
            row17.Append(cell211);
            row17.Append(cell212);
            row17.Append(cell213);
            row17.Append(cell214);
            row17.Append(cell215);
            row17.Append(cell216);
            row17.Append(cell217);
            row17.Append(cell218);
            row17.Append(cell219);
            row17.Append(cell220);
            row17.Append(cell221);

            Row row18 = new Row() { RowIndex = (UInt32Value)18U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell222 = new Cell() { CellReference = "A18", StyleIndex = (UInt32Value)7U };
            Cell cell223 = new Cell() { CellReference = "B18", StyleIndex = (UInt32Value)8U };
            Cell cell224 = new Cell() { CellReference = "C18", StyleIndex = (UInt32Value)8U };
            Cell cell225 = new Cell() { CellReference = "D18", StyleIndex = (UInt32Value)8U };
            Cell cell226 = new Cell() { CellReference = "E18", StyleIndex = (UInt32Value)8U };
            Cell cell227 = new Cell() { CellReference = "F18", StyleIndex = (UInt32Value)8U };
            Cell cell228 = new Cell() { CellReference = "G18", StyleIndex = (UInt32Value)8U };
            Cell cell229 = new Cell() { CellReference = "H18", StyleIndex = (UInt32Value)8U };
            Cell cell230 = new Cell() { CellReference = "I18", StyleIndex = (UInt32Value)8U };
            Cell cell231 = new Cell() { CellReference = "J18", StyleIndex = (UInt32Value)8U };
            Cell cell232 = new Cell() { CellReference = "K18", StyleIndex = (UInt32Value)8U };
            Cell cell233 = new Cell() { CellReference = "L18", StyleIndex = (UInt32Value)8U };
            Cell cell234 = new Cell() { CellReference = "M18", StyleIndex = (UInt32Value)9U };

            row18.Append(cell222);
            row18.Append(cell223);
            row18.Append(cell224);
            row18.Append(cell225);
            row18.Append(cell226);
            row18.Append(cell227);
            row18.Append(cell228);
            row18.Append(cell229);
            row18.Append(cell230);
            row18.Append(cell231);
            row18.Append(cell232);
            row18.Append(cell233);
            row18.Append(cell234);

            Row row19 = new Row() { RowIndex = (UInt32Value)19U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell235 = new Cell() { CellReference = "A19", StyleIndex = (UInt32Value)7U };
            Cell cell236 = new Cell() { CellReference = "B19", StyleIndex = (UInt32Value)8U };
            Cell cell237 = new Cell() { CellReference = "C19", StyleIndex = (UInt32Value)8U };
            Cell cell238 = new Cell() { CellReference = "D19", StyleIndex = (UInt32Value)8U };
            Cell cell239 = new Cell() { CellReference = "E19", StyleIndex = (UInt32Value)8U };
            Cell cell240 = new Cell() { CellReference = "F19", StyleIndex = (UInt32Value)8U };
            Cell cell241 = new Cell() { CellReference = "G19", StyleIndex = (UInt32Value)8U };
            Cell cell242 = new Cell() { CellReference = "H19", StyleIndex = (UInt32Value)8U };
            Cell cell243 = new Cell() { CellReference = "I19", StyleIndex = (UInt32Value)8U };
            Cell cell244 = new Cell() { CellReference = "J19", StyleIndex = (UInt32Value)8U };
            Cell cell245 = new Cell() { CellReference = "K19", StyleIndex = (UInt32Value)8U };
            Cell cell246 = new Cell() { CellReference = "L19", StyleIndex = (UInt32Value)8U };
            Cell cell247 = new Cell() { CellReference = "M19", StyleIndex = (UInt32Value)9U };

            row19.Append(cell235);
            row19.Append(cell236);
            row19.Append(cell237);
            row19.Append(cell238);
            row19.Append(cell239);
            row19.Append(cell240);
            row19.Append(cell241);
            row19.Append(cell242);
            row19.Append(cell243);
            row19.Append(cell244);
            row19.Append(cell245);
            row19.Append(cell246);
            row19.Append(cell247);

            Row row20 = new Row() { RowIndex = (UInt32Value)20U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell248 = new Cell() { CellReference = "A20", StyleIndex = (UInt32Value)7U };
            Cell cell249 = new Cell() { CellReference = "B20", StyleIndex = (UInt32Value)8U };
            Cell cell250 = new Cell() { CellReference = "C20", StyleIndex = (UInt32Value)8U };
            Cell cell251 = new Cell() { CellReference = "D20", StyleIndex = (UInt32Value)8U };
            Cell cell252 = new Cell() { CellReference = "E20", StyleIndex = (UInt32Value)8U };
            Cell cell253 = new Cell() { CellReference = "F20", StyleIndex = (UInt32Value)8U };
            Cell cell254 = new Cell() { CellReference = "G20", StyleIndex = (UInt32Value)8U };
            Cell cell255 = new Cell() { CellReference = "H20", StyleIndex = (UInt32Value)8U };
            Cell cell256 = new Cell() { CellReference = "I20", StyleIndex = (UInt32Value)8U };
            Cell cell257 = new Cell() { CellReference = "J20", StyleIndex = (UInt32Value)8U };
            Cell cell258 = new Cell() { CellReference = "K20", StyleIndex = (UInt32Value)8U };
            Cell cell259 = new Cell() { CellReference = "L20", StyleIndex = (UInt32Value)8U };
            Cell cell260 = new Cell() { CellReference = "M20", StyleIndex = (UInt32Value)9U };

            row20.Append(cell248);
            row20.Append(cell249);
            row20.Append(cell250);
            row20.Append(cell251);
            row20.Append(cell252);
            row20.Append(cell253);
            row20.Append(cell254);
            row20.Append(cell255);
            row20.Append(cell256);
            row20.Append(cell257);
            row20.Append(cell258);
            row20.Append(cell259);
            row20.Append(cell260);

            Row row21 = new Row() { RowIndex = (UInt32Value)21U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell261 = new Cell() { CellReference = "A21", StyleIndex = (UInt32Value)7U };
            Cell cell262 = new Cell() { CellReference = "B21", StyleIndex = (UInt32Value)8U };
            Cell cell263 = new Cell() { CellReference = "C21", StyleIndex = (UInt32Value)8U };
            Cell cell264 = new Cell() { CellReference = "D21", StyleIndex = (UInt32Value)8U };
            Cell cell265 = new Cell() { CellReference = "E21", StyleIndex = (UInt32Value)8U };
            Cell cell266 = new Cell() { CellReference = "F21", StyleIndex = (UInt32Value)8U };
            Cell cell267 = new Cell() { CellReference = "G21", StyleIndex = (UInt32Value)8U };
            Cell cell268 = new Cell() { CellReference = "H21", StyleIndex = (UInt32Value)8U };
            Cell cell269 = new Cell() { CellReference = "I21", StyleIndex = (UInt32Value)8U };
            Cell cell270 = new Cell() { CellReference = "J21", StyleIndex = (UInt32Value)8U };
            Cell cell271 = new Cell() { CellReference = "K21", StyleIndex = (UInt32Value)8U };
            Cell cell272 = new Cell() { CellReference = "L21", StyleIndex = (UInt32Value)8U };
            Cell cell273 = new Cell() { CellReference = "M21", StyleIndex = (UInt32Value)9U };

            row21.Append(cell261);
            row21.Append(cell262);
            row21.Append(cell263);
            row21.Append(cell264);
            row21.Append(cell265);
            row21.Append(cell266);
            row21.Append(cell267);
            row21.Append(cell268);
            row21.Append(cell269);
            row21.Append(cell270);
            row21.Append(cell271);
            row21.Append(cell272);
            row21.Append(cell273);

            Row row22 = new Row() { RowIndex = (UInt32Value)22U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell274 = new Cell() { CellReference = "A22", StyleIndex = (UInt32Value)7U };
            Cell cell275 = new Cell() { CellReference = "B22", StyleIndex = (UInt32Value)8U };
            Cell cell276 = new Cell() { CellReference = "C22", StyleIndex = (UInt32Value)8U };
            Cell cell277 = new Cell() { CellReference = "D22", StyleIndex = (UInt32Value)8U };
            Cell cell278 = new Cell() { CellReference = "E22", StyleIndex = (UInt32Value)8U };
            Cell cell279 = new Cell() { CellReference = "F22", StyleIndex = (UInt32Value)8U };
            Cell cell280 = new Cell() { CellReference = "G22", StyleIndex = (UInt32Value)8U };
            Cell cell281 = new Cell() { CellReference = "H22", StyleIndex = (UInt32Value)8U };
            Cell cell282 = new Cell() { CellReference = "I22", StyleIndex = (UInt32Value)8U };
            Cell cell283 = new Cell() { CellReference = "J22", StyleIndex = (UInt32Value)8U };
            Cell cell284 = new Cell() { CellReference = "K22", StyleIndex = (UInt32Value)8U };
            Cell cell285 = new Cell() { CellReference = "L22", StyleIndex = (UInt32Value)8U };
            Cell cell286 = new Cell() { CellReference = "M22", StyleIndex = (UInt32Value)9U };

            row22.Append(cell274);
            row22.Append(cell275);
            row22.Append(cell276);
            row22.Append(cell277);
            row22.Append(cell278);
            row22.Append(cell279);
            row22.Append(cell280);
            row22.Append(cell281);
            row22.Append(cell282);
            row22.Append(cell283);
            row22.Append(cell284);
            row22.Append(cell285);
            row22.Append(cell286);

            Row row23 = new Row() { RowIndex = (UInt32Value)23U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell287 = new Cell() { CellReference = "A23", StyleIndex = (UInt32Value)7U };
            Cell cell288 = new Cell() { CellReference = "B23", StyleIndex = (UInt32Value)8U };
            Cell cell289 = new Cell() { CellReference = "C23", StyleIndex = (UInt32Value)8U };
            Cell cell290 = new Cell() { CellReference = "D23", StyleIndex = (UInt32Value)8U };
            Cell cell291 = new Cell() { CellReference = "E23", StyleIndex = (UInt32Value)8U };
            Cell cell292 = new Cell() { CellReference = "F23", StyleIndex = (UInt32Value)8U };
            Cell cell293 = new Cell() { CellReference = "G23", StyleIndex = (UInt32Value)8U };
            Cell cell294 = new Cell() { CellReference = "H23", StyleIndex = (UInt32Value)8U };
            Cell cell295 = new Cell() { CellReference = "I23", StyleIndex = (UInt32Value)8U };
            Cell cell296 = new Cell() { CellReference = "J23", StyleIndex = (UInt32Value)8U };
            Cell cell297 = new Cell() { CellReference = "K23", StyleIndex = (UInt32Value)8U };
            Cell cell298 = new Cell() { CellReference = "L23", StyleIndex = (UInt32Value)8U };
            Cell cell299 = new Cell() { CellReference = "M23", StyleIndex = (UInt32Value)9U };

            row23.Append(cell287);
            row23.Append(cell288);
            row23.Append(cell289);
            row23.Append(cell290);
            row23.Append(cell291);
            row23.Append(cell292);
            row23.Append(cell293);
            row23.Append(cell294);
            row23.Append(cell295);
            row23.Append(cell296);
            row23.Append(cell297);
            row23.Append(cell298);
            row23.Append(cell299);

            Row row24 = new Row() { RowIndex = (UInt32Value)24U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell300 = new Cell() { CellReference = "A24", StyleIndex = (UInt32Value)7U };
            Cell cell301 = new Cell() { CellReference = "B24", StyleIndex = (UInt32Value)8U };
            Cell cell302 = new Cell() { CellReference = "C24", StyleIndex = (UInt32Value)8U };
            Cell cell303 = new Cell() { CellReference = "D24", StyleIndex = (UInt32Value)8U };
            Cell cell304 = new Cell() { CellReference = "E24", StyleIndex = (UInt32Value)8U };
            Cell cell305 = new Cell() { CellReference = "F24", StyleIndex = (UInt32Value)8U };
            Cell cell306 = new Cell() { CellReference = "G24", StyleIndex = (UInt32Value)8U };
            Cell cell307 = new Cell() { CellReference = "H24", StyleIndex = (UInt32Value)8U };
            Cell cell308 = new Cell() { CellReference = "I24", StyleIndex = (UInt32Value)8U };
            Cell cell309 = new Cell() { CellReference = "J24", StyleIndex = (UInt32Value)8U };
            Cell cell310 = new Cell() { CellReference = "K24", StyleIndex = (UInt32Value)8U };
            Cell cell311 = new Cell() { CellReference = "L24", StyleIndex = (UInt32Value)8U };
            Cell cell312 = new Cell() { CellReference = "M24", StyleIndex = (UInt32Value)9U };

            row24.Append(cell300);
            row24.Append(cell301);
            row24.Append(cell302);
            row24.Append(cell303);
            row24.Append(cell304);
            row24.Append(cell305);
            row24.Append(cell306);
            row24.Append(cell307);
            row24.Append(cell308);
            row24.Append(cell309);
            row24.Append(cell310);
            row24.Append(cell311);
            row24.Append(cell312);

            Row row25 = new Row() { RowIndex = (UInt32Value)25U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell313 = new Cell() { CellReference = "A25", StyleIndex = (UInt32Value)7U };
            Cell cell314 = new Cell() { CellReference = "B25", StyleIndex = (UInt32Value)8U };
            Cell cell315 = new Cell() { CellReference = "C25", StyleIndex = (UInt32Value)8U };
            Cell cell316 = new Cell() { CellReference = "D25", StyleIndex = (UInt32Value)8U };
            Cell cell317 = new Cell() { CellReference = "E25", StyleIndex = (UInt32Value)8U };
            Cell cell318 = new Cell() { CellReference = "F25", StyleIndex = (UInt32Value)8U };
            Cell cell319 = new Cell() { CellReference = "G25", StyleIndex = (UInt32Value)8U };
            Cell cell320 = new Cell() { CellReference = "H25", StyleIndex = (UInt32Value)8U };
            Cell cell321 = new Cell() { CellReference = "I25", StyleIndex = (UInt32Value)8U };
            Cell cell322 = new Cell() { CellReference = "J25", StyleIndex = (UInt32Value)8U };
            Cell cell323 = new Cell() { CellReference = "K25", StyleIndex = (UInt32Value)8U };
            Cell cell324 = new Cell() { CellReference = "L25", StyleIndex = (UInt32Value)8U };
            Cell cell325 = new Cell() { CellReference = "M25", StyleIndex = (UInt32Value)9U };

            row25.Append(cell313);
            row25.Append(cell314);
            row25.Append(cell315);
            row25.Append(cell316);
            row25.Append(cell317);
            row25.Append(cell318);
            row25.Append(cell319);
            row25.Append(cell320);
            row25.Append(cell321);
            row25.Append(cell322);
            row25.Append(cell323);
            row25.Append(cell324);
            row25.Append(cell325);

            Row row26 = new Row() { RowIndex = (UInt32Value)26U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell326 = new Cell() { CellReference = "A26", StyleIndex = (UInt32Value)7U };
            Cell cell327 = new Cell() { CellReference = "B26", StyleIndex = (UInt32Value)8U };
            Cell cell328 = new Cell() { CellReference = "C26", StyleIndex = (UInt32Value)8U };
            Cell cell329 = new Cell() { CellReference = "D26", StyleIndex = (UInt32Value)8U };
            Cell cell330 = new Cell() { CellReference = "E26", StyleIndex = (UInt32Value)8U };
            Cell cell331 = new Cell() { CellReference = "F26", StyleIndex = (UInt32Value)8U };
            Cell cell332 = new Cell() { CellReference = "G26", StyleIndex = (UInt32Value)8U };
            Cell cell333 = new Cell() { CellReference = "H26", StyleIndex = (UInt32Value)8U };
            Cell cell334 = new Cell() { CellReference = "I26", StyleIndex = (UInt32Value)8U };
            Cell cell335 = new Cell() { CellReference = "J26", StyleIndex = (UInt32Value)8U };
            Cell cell336 = new Cell() { CellReference = "K26", StyleIndex = (UInt32Value)8U };
            Cell cell337 = new Cell() { CellReference = "L26", StyleIndex = (UInt32Value)8U };
            Cell cell338 = new Cell() { CellReference = "M26", StyleIndex = (UInt32Value)9U };

            row26.Append(cell326);
            row26.Append(cell327);
            row26.Append(cell328);
            row26.Append(cell329);
            row26.Append(cell330);
            row26.Append(cell331);
            row26.Append(cell332);
            row26.Append(cell333);
            row26.Append(cell334);
            row26.Append(cell335);
            row26.Append(cell336);
            row26.Append(cell337);
            row26.Append(cell338);

            Row row27 = new Row() { RowIndex = (UInt32Value)27U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell339 = new Cell() { CellReference = "A27", StyleIndex = (UInt32Value)7U };
            Cell cell340 = new Cell() { CellReference = "B27", StyleIndex = (UInt32Value)8U };
            Cell cell341 = new Cell() { CellReference = "C27", StyleIndex = (UInt32Value)8U };
            Cell cell342 = new Cell() { CellReference = "D27", StyleIndex = (UInt32Value)8U };
            Cell cell343 = new Cell() { CellReference = "E27", StyleIndex = (UInt32Value)8U };
            Cell cell344 = new Cell() { CellReference = "F27", StyleIndex = (UInt32Value)8U };
            Cell cell345 = new Cell() { CellReference = "G27", StyleIndex = (UInt32Value)8U };
            Cell cell346 = new Cell() { CellReference = "H27", StyleIndex = (UInt32Value)8U };
            Cell cell347 = new Cell() { CellReference = "I27", StyleIndex = (UInt32Value)8U };
            Cell cell348 = new Cell() { CellReference = "J27", StyleIndex = (UInt32Value)8U };
            Cell cell349 = new Cell() { CellReference = "K27", StyleIndex = (UInt32Value)8U };
            Cell cell350 = new Cell() { CellReference = "L27", StyleIndex = (UInt32Value)8U };
            Cell cell351 = new Cell() { CellReference = "M27", StyleIndex = (UInt32Value)9U };

            row27.Append(cell339);
            row27.Append(cell340);
            row27.Append(cell341);
            row27.Append(cell342);
            row27.Append(cell343);
            row27.Append(cell344);
            row27.Append(cell345);
            row27.Append(cell346);
            row27.Append(cell347);
            row27.Append(cell348);
            row27.Append(cell349);
            row27.Append(cell350);
            row27.Append(cell351);

            Row row28 = new Row() { RowIndex = (UInt32Value)28U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell352 = new Cell() { CellReference = "A28", StyleIndex = (UInt32Value)7U };
            Cell cell353 = new Cell() { CellReference = "B28", StyleIndex = (UInt32Value)8U };
            Cell cell354 = new Cell() { CellReference = "C28", StyleIndex = (UInt32Value)8U };
            Cell cell355 = new Cell() { CellReference = "D28", StyleIndex = (UInt32Value)8U };
            Cell cell356 = new Cell() { CellReference = "E28", StyleIndex = (UInt32Value)8U };
            Cell cell357 = new Cell() { CellReference = "F28", StyleIndex = (UInt32Value)8U };
            Cell cell358 = new Cell() { CellReference = "G28", StyleIndex = (UInt32Value)8U };
            Cell cell359 = new Cell() { CellReference = "H28", StyleIndex = (UInt32Value)8U };
            Cell cell360 = new Cell() { CellReference = "I28", StyleIndex = (UInt32Value)8U };
            Cell cell361 = new Cell() { CellReference = "J28", StyleIndex = (UInt32Value)8U };
            Cell cell362 = new Cell() { CellReference = "K28", StyleIndex = (UInt32Value)8U };
            Cell cell363 = new Cell() { CellReference = "L28", StyleIndex = (UInt32Value)8U };
            Cell cell364 = new Cell() { CellReference = "M28", StyleIndex = (UInt32Value)9U };

            row28.Append(cell352);
            row28.Append(cell353);
            row28.Append(cell354);
            row28.Append(cell355);
            row28.Append(cell356);
            row28.Append(cell357);
            row28.Append(cell358);
            row28.Append(cell359);
            row28.Append(cell360);
            row28.Append(cell361);
            row28.Append(cell362);
            row28.Append(cell363);
            row28.Append(cell364);

            Row row29 = new Row() { RowIndex = (UInt32Value)29U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell365 = new Cell() { CellReference = "A29", StyleIndex = (UInt32Value)7U };
            Cell cell366 = new Cell() { CellReference = "B29", StyleIndex = (UInt32Value)8U };
            Cell cell367 = new Cell() { CellReference = "C29", StyleIndex = (UInt32Value)8U };
            Cell cell368 = new Cell() { CellReference = "D29", StyleIndex = (UInt32Value)8U };
            Cell cell369 = new Cell() { CellReference = "E29", StyleIndex = (UInt32Value)8U };
            Cell cell370 = new Cell() { CellReference = "F29", StyleIndex = (UInt32Value)8U };
            Cell cell371 = new Cell() { CellReference = "G29", StyleIndex = (UInt32Value)8U };
            Cell cell372 = new Cell() { CellReference = "H29", StyleIndex = (UInt32Value)8U };
            Cell cell373 = new Cell() { CellReference = "I29", StyleIndex = (UInt32Value)8U };
            Cell cell374 = new Cell() { CellReference = "J29", StyleIndex = (UInt32Value)8U };
            Cell cell375 = new Cell() { CellReference = "K29", StyleIndex = (UInt32Value)8U };
            Cell cell376 = new Cell() { CellReference = "L29", StyleIndex = (UInt32Value)8U };
            Cell cell377 = new Cell() { CellReference = "M29", StyleIndex = (UInt32Value)9U };

            row29.Append(cell365);
            row29.Append(cell366);
            row29.Append(cell367);
            row29.Append(cell368);
            row29.Append(cell369);
            row29.Append(cell370);
            row29.Append(cell371);
            row29.Append(cell372);
            row29.Append(cell373);
            row29.Append(cell374);
            row29.Append(cell375);
            row29.Append(cell376);
            row29.Append(cell377);

            Row row30 = new Row() { RowIndex = (UInt32Value)30U, Spans = new ListValue<StringValue>() { InnerText = "1:13" }, DyDescent = 0.25D };
            Cell cell378 = new Cell() { CellReference = "A30", StyleIndex = (UInt32Value)10U };
            Cell cell379 = new Cell() { CellReference = "B30", StyleIndex = (UInt32Value)11U };
            Cell cell380 = new Cell() { CellReference = "C30", StyleIndex = (UInt32Value)11U };
            Cell cell381 = new Cell() { CellReference = "D30", StyleIndex = (UInt32Value)11U };
            Cell cell382 = new Cell() { CellReference = "E30", StyleIndex = (UInt32Value)11U };
            Cell cell383 = new Cell() { CellReference = "F30", StyleIndex = (UInt32Value)11U };
            Cell cell384 = new Cell() { CellReference = "G30", StyleIndex = (UInt32Value)11U };
            Cell cell385 = new Cell() { CellReference = "H30", StyleIndex = (UInt32Value)11U };
            Cell cell386 = new Cell() { CellReference = "I30", StyleIndex = (UInt32Value)11U };
            Cell cell387 = new Cell() { CellReference = "J30", StyleIndex = (UInt32Value)11U };
            Cell cell388 = new Cell() { CellReference = "K30", StyleIndex = (UInt32Value)11U };
            Cell cell389 = new Cell() { CellReference = "L30", StyleIndex = (UInt32Value)11U };
            Cell cell390 = new Cell() { CellReference = "M30", StyleIndex = (UInt32Value)12U };

            row30.Append(cell378);
            row30.Append(cell379);
            row30.Append(cell380);
            row30.Append(cell381);
            row30.Append(cell382);
            row30.Append(cell383);
            row30.Append(cell384);
            row30.Append(cell385);
            row30.Append(cell386);
            row30.Append(cell387);
            row30.Append(cell388);
            row30.Append(cell389);
            row30.Append(cell390);

            Row row31 = new Row() { RowIndex = (UInt32Value)33U, Spans = new ListValue<StringValue>() { InnerText = "1:9" }, DyDescent = 0.25D };

            Cell cell391 = new Cell() { CellReference = "A33", StyleIndex = (UInt32Value)1U, DataType = CellValues.SharedString };
            CellValue cellValue1 = new CellValue();
            cellValue1.Text = "50";

            cell391.Append(cellValue1);

            Cell cell392 = new Cell() { CellReference = "B33", StyleIndex = (UInt32Value)1U, DataType = CellValues.SharedString };
            CellValue cellValue2 = new CellValue();
            cellValue2.Text = "52";

            cell392.Append(cellValue2);

            row31.Append(cell391);
            row31.Append(cell392);

            Row row32 = new Row() { RowIndex = (UInt32Value)34U, Spans = new ListValue<StringValue>() { InnerText = "1:9" }, DyDescent = 0.25D };

            Cell cell393 = new Cell() { CellReference = "A34", StyleIndex = (UInt32Value)1U, DataType = CellValues.SharedString };
            CellValue cellValue3 = new CellValue();
            cellValue3.Text = "49";

            cell393.Append(cellValue3);

            Cell cell394 = new Cell() { CellReference = "B34", DataType = CellValues.SharedString };
            CellValue cellValue4 = new CellValue();
            cellValue4.Text = "48";

            cell394.Append(cellValue4);

            Cell cell395 = new Cell() { CellReference = "C34", DataType = CellValues.SharedString };
            CellValue cellValue5 = new CellValue();
            cellValue5.Text = "27";

            cell395.Append(cellValue5);

            Cell cell396 = new Cell() { CellReference = "D34", DataType = CellValues.SharedString };
            CellValue cellValue6 = new CellValue();
            cellValue6.Text = "20";

            cell396.Append(cellValue6);

            Cell cell397 = new Cell() { CellReference = "E34", DataType = CellValues.SharedString };
            CellValue cellValue7 = new CellValue();
            cellValue7.Text = "51";

            cell397.Append(cellValue7);

            Cell cell398 = new Cell() { CellReference = "H34", StyleIndex = (UInt32Value)1U, DataType = CellValues.SharedString };
            CellValue cellValue8 = new CellValue();
            cellValue8.Text = "49";

            cell398.Append(cellValue8);

            Cell cell399 = new Cell() { CellReference = "I34", DataType = CellValues.SharedString };
            CellValue cellValue9 = new CellValue();
            cellValue9.Text = "50";

            cell399.Append(cellValue9);

            row32.Append(cell393);
            row32.Append(cell394);
            row32.Append(cell395);
            row32.Append(cell396);
            row32.Append(cell397);
            row32.Append(cell398);
            row32.Append(cell399);

            Row row33 = new Row() { RowIndex = (UInt32Value)35U, Spans = new ListValue<StringValue>() { InnerText = "1:9" }, DyDescent = 0.25D };

            Cell cell400 = new Cell() { CellReference = "A35", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue10 = new CellValue();
            cellValue10.Text = "1";

            cell400.Append(cellValue10);
            Cell cell401 = new Cell() { CellReference = "B35", StyleIndex = (UInt32Value)3U };
            Cell cell402 = new Cell() { CellReference = "C35", StyleIndex = (UInt32Value)3U };

            Cell cell403 = new Cell() { CellReference = "D35", StyleIndex = (UInt32Value)3U };
            CellValue cellValue11 = new CellValue();
            cellValue11.Text = "1";

            cell403.Append(cellValue11);

            Cell cell404 = new Cell() { CellReference = "E35", StyleIndex = (UInt32Value)3U };
            CellValue cellValue12 = new CellValue();
            cellValue12.Text = "1";

            cell404.Append(cellValue12);

            Cell cell405 = new Cell() { CellReference = "H35", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue13 = new CellValue();
            cellValue13.Text = "48";

            cell405.Append(cellValue13);

            Cell cell406 = new Cell() { CellReference = "I35", StyleIndex = (UInt32Value)3U };
            CellValue cellValue14 = new CellValue();
            cellValue14.Text = "1";

            cell406.Append(cellValue14);

            row33.Append(cell400);
            row33.Append(cell401);
            row33.Append(cell402);
            row33.Append(cell403);
            row33.Append(cell404);
            row33.Append(cell405);
            row33.Append(cell406);

            Row row34 = new Row() { RowIndex = (UInt32Value)36U, Spans = new ListValue<StringValue>() { InnerText = "1:9" }, DyDescent = 0.25D };

            Cell cell407 = new Cell() { CellReference = "A36", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue15 = new CellValue();
            cellValue15.Text = "2";

            cell407.Append(cellValue15);
            Cell cell408 = new Cell() { CellReference = "B36", StyleIndex = (UInt32Value)3U };

            Cell cell409 = new Cell() { CellReference = "C36", StyleIndex = (UInt32Value)3U };
            CellValue cellValue16 = new CellValue();
            cellValue16.Text = "2";

            cell409.Append(cellValue16);

            Cell cell410 = new Cell() { CellReference = "D36", StyleIndex = (UInt32Value)3U };
            CellValue cellValue17 = new CellValue();
            cellValue17.Text = "2";

            cell410.Append(cellValue17);

            Cell cell411 = new Cell() { CellReference = "E36", StyleIndex = (UInt32Value)3U };
            CellValue cellValue18 = new CellValue();
            cellValue18.Text = "4";

            cell411.Append(cellValue18);

            Cell cell412 = new Cell() { CellReference = "H36", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue19 = new CellValue();
            cellValue19.Text = "27";

            cell412.Append(cellValue19);

            Cell cell413 = new Cell() { CellReference = "I36", StyleIndex = (UInt32Value)3U };
            CellValue cellValue20 = new CellValue();
            cellValue20.Text = "3";

            cell413.Append(cellValue20);

            row34.Append(cell407);
            row34.Append(cell408);
            row34.Append(cell409);
            row34.Append(cell410);
            row34.Append(cell411);
            row34.Append(cell412);
            row34.Append(cell413);

            Row row35 = new Row() { RowIndex = (UInt32Value)37U, Spans = new ListValue<StringValue>() { InnerText = "1:9" }, DyDescent = 0.25D };

            Cell cell414 = new Cell() { CellReference = "A37", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue21 = new CellValue();
            cellValue21.Text = "3";

            cell414.Append(cellValue21);

            Cell cell415 = new Cell() { CellReference = "B37", StyleIndex = (UInt32Value)3U };
            CellValue cellValue22 = new CellValue();
            cellValue22.Text = "1";

            cell415.Append(cellValue22);

            Cell cell416 = new Cell() { CellReference = "C37", StyleIndex = (UInt32Value)3U };
            CellValue cellValue23 = new CellValue();
            cellValue23.Text = "1";

            cell416.Append(cellValue23);
            Cell cell417 = new Cell() { CellReference = "D37", StyleIndex = (UInt32Value)3U };

            Cell cell418 = new Cell() { CellReference = "E37", StyleIndex = (UInt32Value)3U };
            CellValue cellValue24 = new CellValue();
            cellValue24.Text = "2";

            cell418.Append(cellValue24);

            Cell cell419 = new Cell() { CellReference = "H37", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue25 = new CellValue();
            cellValue25.Text = "20";

            cell419.Append(cellValue25);

            Cell cell420 = new Cell() { CellReference = "I37", StyleIndex = (UInt32Value)3U };
            CellValue cellValue26 = new CellValue();
            cellValue26.Text = "3";

            cell420.Append(cellValue26);

            row35.Append(cell414);
            row35.Append(cell415);
            row35.Append(cell416);
            row35.Append(cell417);
            row35.Append(cell418);
            row35.Append(cell419);
            row35.Append(cell420);

            Row row36 = new Row() { RowIndex = (UInt32Value)38U, Spans = new ListValue<StringValue>() { InnerText = "1:9" }, DyDescent = 0.25D };

            Cell cell421 = new Cell() { CellReference = "A38", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue27 = new CellValue();
            cellValue27.Text = "51";

            cell421.Append(cellValue27);

            Cell cell422 = new Cell() { CellReference = "B38", StyleIndex = (UInt32Value)3U };
            CellValue cellValue28 = new CellValue();
            cellValue28.Text = "1";

            cell422.Append(cellValue28);

            Cell cell423 = new Cell() { CellReference = "C38", StyleIndex = (UInt32Value)3U };
            CellValue cellValue29 = new CellValue();
            cellValue29.Text = "3";

            cell423.Append(cellValue29);

            Cell cell424 = new Cell() { CellReference = "D38", StyleIndex = (UInt32Value)3U };
            CellValue cellValue30 = new CellValue();
            cellValue30.Text = "3";

            cell424.Append(cellValue30);

            Cell cell425 = new Cell() { CellReference = "E38", StyleIndex = (UInt32Value)3U };
            CellValue cellValue31 = new CellValue();
            cellValue31.Text = "7";

            cell425.Append(cellValue31);

            Cell cell426 = new Cell() { CellReference = "H38", StyleIndex = (UInt32Value)2U, DataType = CellValues.SharedString };
            CellValue cellValue32 = new CellValue();
            cellValue32.Text = "51";

            cell426.Append(cellValue32);

            Cell cell427 = new Cell() { CellReference = "I38", StyleIndex = (UInt32Value)3U };
            CellValue cellValue33 = new CellValue();
            cellValue33.Text = "7";

            cell427.Append(cellValue33);

            row36.Append(cell421);
            row36.Append(cell422);
            row36.Append(cell423);
            row36.Append(cell424);
            row36.Append(cell425);
            row36.Append(cell426);
            row36.Append(cell427);

            sheetData1.Append(row1);
            sheetData1.Append(row2);
            sheetData1.Append(row3);
            sheetData1.Append(row4);
            sheetData1.Append(row5);
            sheetData1.Append(row6);
            sheetData1.Append(row7);
            sheetData1.Append(row8);
            sheetData1.Append(row9);
            sheetData1.Append(row10);
            sheetData1.Append(row11);
            sheetData1.Append(row12);
            sheetData1.Append(row13);
            sheetData1.Append(row14);
            sheetData1.Append(row15);
            sheetData1.Append(row16);
            sheetData1.Append(row17);
            sheetData1.Append(row18);
            sheetData1.Append(row19);
            sheetData1.Append(row20);
            sheetData1.Append(row21);
            sheetData1.Append(row22);
            sheetData1.Append(row23);
            sheetData1.Append(row24);
            sheetData1.Append(row25);
            sheetData1.Append(row26);
            sheetData1.Append(row27);
            sheetData1.Append(row28);
            sheetData1.Append(row29);
            sheetData1.Append(row30);
            sheetData1.Append(row31);
            sheetData1.Append(row32);
            sheetData1.Append(row33);
            sheetData1.Append(row34);
            sheetData1.Append(row35);
            sheetData1.Append(row36);
            PageMargins pageMargins1 = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            //PageSetup pageSetup1 = new PageSetup() { Orientation = OrientationValues.Portrait, Id = "rId3" };
            Drawing drawing1 = new Drawing() { Id = "rId4" };

            worksheet1.Append(sheetDimension1);
            worksheet1.Append(sheetViews1);
            worksheet1.Append(sheetFormatProperties1);
            worksheet1.Append(columns1);
            worksheet1.Append(sheetData1);
            worksheet1.Append(pageMargins1);
            //worksheet1.Append(pageSetup1);
            worksheet1.Append(drawing1);

            worksheetPart1.Worksheet = worksheet1;
        }

        // Generates content of spreadsheetPrinterSettingsPart1.
        //private void GenerateSpreadsheetPrinterSettingsPart1Content(SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart1)
        //{
        //    System.IO.Stream data = GetBinaryDataStream(spreadsheetPrinterSettingsPart1Data);
        //    spreadsheetPrinterSettingsPart1.FeedData(data);
        //    data.Close();
        //}

        // Generates content of pivotTablePart1.
        private void GeneratePivotTablePart1Content(PivotTablePart pivotTablePart1)
        {
            PivotTableDefinition pivotTableDefinition1 = new PivotTableDefinition() { Name = "PivotTable8", CacheId = (UInt32Value)4U, ApplyNumberFormats = false, ApplyBorderFormats = false, ApplyFontFormats = false, ApplyPatternFormats = false, ApplyAlignmentFormats = false, ApplyWidthHeightFormats = true, DataCaption = "Values", UpdatedVersion = 6, MinRefreshableVersion = 3, UseAutoFormatting = true, ItemPrintTitles = true, CreatedVersion = 6, Indent = (UInt32Value)0U, Outline = true, OutlineData = true, MultipleFieldFilters = false, ChartFormat = (UInt32Value)9U };
            pivotTableDefinition1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //pivotTableDefinition1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //pivotTableDefinition1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{B1B5737E-B1A4-4B51-B7EB-4C8183015397}"));
            DocumentFormat.OpenXml.Spreadsheet.Location location1 = new DocumentFormat.OpenXml.Spreadsheet.Location() { Reference = "A33:E38", FirstHeaderRow = (UInt32Value)1U, FirstDataRow = (UInt32Value)2U, FirstDataColumn = (UInt32Value)1U };

            PivotFields pivotFields1 = new PivotFields() { Count = (UInt32Value)10U };
            PivotField pivotField1 = new PivotField() { DataField = true, ShowAll = false };
            PivotField pivotField2 = new PivotField() { ShowAll = false };
            PivotField pivotField3 = new PivotField() { ShowAll = false };

            PivotField pivotField4 = new PivotField() { Axis = PivotTableAxisValues.AxisRow, ShowAll = false };

            Items items1 = new Items() { Count = (UInt32Value)4U };
            Item item1 = new Item() { Index = (UInt32Value)0U };
            Item item2 = new Item() { Index = (UInt32Value)2U };
            Item item3 = new Item() { Index = (UInt32Value)1U };
            Item item4 = new Item() { ItemType = ItemValues.Default };

            items1.Append(item1);
            items1.Append(item2);
            items1.Append(item3);
            items1.Append(item4);

            pivotField4.Append(items1);

            PivotField pivotField5 = new PivotField() { Axis = PivotTableAxisValues.AxisColumn, ShowAll = false };

            Items items2 = new Items() { Count = (UInt32Value)4U };
            Item item5 = new Item() { Index = (UInt32Value)2U };
            Item item6 = new Item() { Index = (UInt32Value)1U };
            Item item7 = new Item() { Index = (UInt32Value)0U };
            Item item8 = new Item() { ItemType = ItemValues.Default };

            items2.Append(item5);
            items2.Append(item6);
            items2.Append(item7);
            items2.Append(item8);

            pivotField5.Append(items2);
            PivotField pivotField6 = new PivotField() { ShowAll = false };
            PivotField pivotField7 = new PivotField() { ShowAll = false };
            PivotField pivotField8 = new PivotField() { ShowAll = false };
            PivotField pivotField9 = new PivotField() { ShowAll = false };
            PivotField pivotField10 = new PivotField() { ShowAll = false };

            pivotFields1.Append(pivotField1);
            pivotFields1.Append(pivotField2);
            pivotFields1.Append(pivotField3);
            pivotFields1.Append(pivotField4);
            pivotFields1.Append(pivotField5);
            pivotFields1.Append(pivotField6);
            pivotFields1.Append(pivotField7);
            pivotFields1.Append(pivotField8);
            pivotFields1.Append(pivotField9);
            pivotFields1.Append(pivotField10);

            RowFields rowFields1 = new RowFields() { Count = (UInt32Value)1U };
            Field field1 = new Field() { Index = 3 };

            rowFields1.Append(field1);

            RowItems rowItems1 = new RowItems() { Count = (UInt32Value)4U };

            RowItem rowItem1 = new RowItem();
            MemberPropertyIndex memberPropertyIndex1 = new MemberPropertyIndex();

            rowItem1.Append(memberPropertyIndex1);

            RowItem rowItem2 = new RowItem();
            MemberPropertyIndex memberPropertyIndex2 = new MemberPropertyIndex() { Val = 1 };

            rowItem2.Append(memberPropertyIndex2);

            RowItem rowItem3 = new RowItem();
            MemberPropertyIndex memberPropertyIndex3 = new MemberPropertyIndex() { Val = 2 };

            rowItem3.Append(memberPropertyIndex3);

            RowItem rowItem4 = new RowItem() { ItemType = ItemValues.Grand };
            MemberPropertyIndex memberPropertyIndex4 = new MemberPropertyIndex();

            rowItem4.Append(memberPropertyIndex4);

            rowItems1.Append(rowItem1);
            rowItems1.Append(rowItem2);
            rowItems1.Append(rowItem3);
            rowItems1.Append(rowItem4);

            ColumnFields columnFields1 = new ColumnFields() { Count = (UInt32Value)1U };
            Field field2 = new Field() { Index = 4 };

            columnFields1.Append(field2);

            ColumnItems columnItems1 = new ColumnItems() { Count = (UInt32Value)4U };

            RowItem rowItem5 = new RowItem();
            MemberPropertyIndex memberPropertyIndex5 = new MemberPropertyIndex();

            rowItem5.Append(memberPropertyIndex5);

            RowItem rowItem6 = new RowItem();
            MemberPropertyIndex memberPropertyIndex6 = new MemberPropertyIndex() { Val = 1 };

            rowItem6.Append(memberPropertyIndex6);

            RowItem rowItem7 = new RowItem();
            MemberPropertyIndex memberPropertyIndex7 = new MemberPropertyIndex() { Val = 2 };

            rowItem7.Append(memberPropertyIndex7);

            RowItem rowItem8 = new RowItem() { ItemType = ItemValues.Grand };
            MemberPropertyIndex memberPropertyIndex8 = new MemberPropertyIndex();

            rowItem8.Append(memberPropertyIndex8);

            columnItems1.Append(rowItem5);
            columnItems1.Append(rowItem6);
            columnItems1.Append(rowItem7);
            columnItems1.Append(rowItem8);

            DataFields dataFields1 = new DataFields() { Count = (UInt32Value)1U };
            DataField dataField1 = new DataField() { Name = "Count of Task", Field = (UInt32Value)0U, Subtotal = DataConsolidateFunctionValues.Count, BaseField = 0, BaseItem = (UInt32Value)0U };

            dataFields1.Append(dataField1);

            ChartFormats chartFormats1 = new ChartFormats() { Count = (UInt32Value)3U };

            ChartFormat chartFormat1 = new ChartFormat() { Chart = (UInt32Value)8U, Format = (UInt32Value)0U, Series = true };

            PivotArea pivotArea1 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences1 = new PivotAreaReferences() { Count = (UInt32Value)2U };

            PivotAreaReference pivotAreaReference1 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem1 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference1.Append(fieldItem1);

            PivotAreaReference pivotAreaReference2 = new PivotAreaReference() { Field = (UInt32Value)4U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem2 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference2.Append(fieldItem2);

            pivotAreaReferences1.Append(pivotAreaReference1);
            pivotAreaReferences1.Append(pivotAreaReference2);

            pivotArea1.Append(pivotAreaReferences1);

            chartFormat1.Append(pivotArea1);

            ChartFormat chartFormat2 = new ChartFormat() { Chart = (UInt32Value)8U, Format = (UInt32Value)1U, Series = true };

            PivotArea pivotArea2 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences2 = new PivotAreaReferences() { Count = (UInt32Value)2U };

            PivotAreaReference pivotAreaReference3 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem3 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference3.Append(fieldItem3);

            PivotAreaReference pivotAreaReference4 = new PivotAreaReference() { Field = (UInt32Value)4U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem4 = new FieldItem() { Val = (UInt32Value)1U };

            pivotAreaReference4.Append(fieldItem4);

            pivotAreaReferences2.Append(pivotAreaReference3);
            pivotAreaReferences2.Append(pivotAreaReference4);

            pivotArea2.Append(pivotAreaReferences2);

            chartFormat2.Append(pivotArea2);

            ChartFormat chartFormat3 = new ChartFormat() { Chart = (UInt32Value)8U, Format = (UInt32Value)2U, Series = true };

            PivotArea pivotArea3 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences3 = new PivotAreaReferences() { Count = (UInt32Value)2U };

            PivotAreaReference pivotAreaReference5 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem5 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference5.Append(fieldItem5);

            PivotAreaReference pivotAreaReference6 = new PivotAreaReference() { Field = (UInt32Value)4U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem6 = new FieldItem() { Val = (UInt32Value)2U };

            pivotAreaReference6.Append(fieldItem6);

            pivotAreaReferences3.Append(pivotAreaReference5);
            pivotAreaReferences3.Append(pivotAreaReference6);

            pivotArea3.Append(pivotAreaReferences3);

            chartFormat3.Append(pivotArea3);

            chartFormats1.Append(chartFormat1);
            chartFormats1.Append(chartFormat2);
            chartFormats1.Append(chartFormat3);
            PivotTableStyle pivotTableStyle1 = new PivotTableStyle() { Name = "PivotStyleMedium9", ShowRowHeaders = true, ShowColumnHeaders = true, ShowRowStripes = false, ShowColumnStripes = false, ShowLastColumn = true };

            PivotTableDefinitionExtensionList pivotTableDefinitionExtensionList1 = new PivotTableDefinitionExtensionList();

            PivotTableDefinitionExtension pivotTableDefinitionExtension1 = new PivotTableDefinitionExtension() { Uri = "{962EF5D1-5CA2-4c93-8EF4-DBF5C05439D2}" };
            pivotTableDefinitionExtension1.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");

            X14.PivotTableDefinition pivotTableDefinition2 = new X14.PivotTableDefinition() { HideValuesRow = true };
            pivotTableDefinition2.AddNamespaceDeclaration("xm", "http://schemas.microsoft.com/office/excel/2006/main");

            pivotTableDefinitionExtension1.Append(pivotTableDefinition2);

            PivotTableDefinitionExtension pivotTableDefinitionExtension2 = new PivotTableDefinitionExtension() { Uri = "{747A6164-185A-40DC-8AA5-F01512510D54}" };
            pivotTableDefinitionExtension2.AddNamespaceDeclaration("xpdl", "http://schemas.microsoft.com/office/spreadsheetml/2016/pivotdefaultlayout");
            OpenXmlUnknownElement openXmlUnknownElement4 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<xpdl:pivotTableDefinition16 xmlns:xpdl=\"http://schemas.microsoft.com/office/spreadsheetml/2016/pivotdefaultlayout\" />");

            pivotTableDefinitionExtension2.Append(openXmlUnknownElement4);

            pivotTableDefinitionExtensionList1.Append(pivotTableDefinitionExtension1);
            pivotTableDefinitionExtensionList1.Append(pivotTableDefinitionExtension2);

            pivotTableDefinition1.Append(location1);
            pivotTableDefinition1.Append(pivotFields1);
            pivotTableDefinition1.Append(rowFields1);
            pivotTableDefinition1.Append(rowItems1);
            pivotTableDefinition1.Append(columnFields1);
            pivotTableDefinition1.Append(columnItems1);
            pivotTableDefinition1.Append(dataFields1);
            pivotTableDefinition1.Append(chartFormats1);
            pivotTableDefinition1.Append(pivotTableStyle1);
            pivotTableDefinition1.Append(pivotTableDefinitionExtensionList1);

            pivotTablePart1.PivotTableDefinition = pivotTableDefinition1;
        }

        // Generates content of pivotTableCacheDefinitionPart1.
        private void GeneratePivotTableCacheDefinitionPart1Content(PivotTableCacheDefinitionPart pivotTableCacheDefinitionPart1)
        {
            PivotCacheDefinition pivotCacheDefinition1 = new PivotCacheDefinition() { Id = "rId1", RefreshedBy = "Tom Jebo", RefreshedDate = 43747.681090162034D, CreatedVersion = 6, RefreshedVersion = 6, MinRefreshableVersion = 3, RecordCount = (UInt32Value)7U };
            pivotCacheDefinition1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            pivotCacheDefinition1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //pivotCacheDefinition1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //pivotCacheDefinition1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{EB244034-01A9-4680-A3DF-D8D769D5F69B}"));

            CacheSource cacheSource1 = new CacheSource() { Type = SourceValues.Worksheet };
            WorksheetSource worksheetSource1 = new WorksheetSource() { Name = "WorkItemsTable" };

            cacheSource1.Append(worksheetSource1);

            CacheFields cacheFields1 = new CacheFields() { Count = (UInt32Value)10U };

            CacheField cacheField1 = new CacheField() { Name = "Task", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems1 = new SharedItems();

            cacheField1.Append(sharedItems1);

            CacheField cacheField2 = new CacheField() { Name = "Owner", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems2 = new SharedItems();

            cacheField2.Append(sharedItems2);

            CacheField cacheField3 = new CacheField() { Name = "Email", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems3 = new SharedItems();

            cacheField3.Append(sharedItems3);

            CacheField cacheField4 = new CacheField() { Name = "Bucket", NumberFormatId = (UInt32Value)0U };

            SharedItems sharedItems4 = new SharedItems() { Count = (UInt32Value)3U };
            StringItem stringItem1 = new StringItem() { Val = "ChemCo Distilling Visit" };
            StringItem stringItem2 = new StringItem() { Val = "Redmond Site Visit" };
            StringItem stringItem3 = new StringItem() { Val = "Focus Bucket" };

            sharedItems4.Append(stringItem1);
            sharedItems4.Append(stringItem2);
            sharedItems4.Append(stringItem3);

            cacheField4.Append(sharedItems4);

            CacheField cacheField5 = new CacheField() { Name = "Progress", NumberFormatId = (UInt32Value)0U };

            SharedItems sharedItems5 = new SharedItems() { Count = (UInt32Value)3U };
            StringItem stringItem4 = new StringItem() { Val = "Not started" };
            StringItem stringItem5 = new StringItem() { Val = "In Progress" };
            StringItem stringItem6 = new StringItem() { Val = "Completed" };

            sharedItems5.Append(stringItem4);
            sharedItems5.Append(stringItem5);
            sharedItems5.Append(stringItem6);

            cacheField5.Append(sharedItems5);

            CacheField cacheField6 = new CacheField() { Name = "Due Date", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems6 = new SharedItems();

            cacheField6.Append(sharedItems6);

            CacheField cacheField7 = new CacheField() { Name = "Completed Date", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems7 = new SharedItems() { ContainsBlank = true };

            cacheField7.Append(sharedItems7);

            CacheField cacheField8 = new CacheField() { Name = "Completed By", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems8 = new SharedItems() { ContainsNonDate = false, ContainsString = false, ContainsBlank = true };

            cacheField8.Append(sharedItems8);

            CacheField cacheField9 = new CacheField() { Name = "Created Date", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems9 = new SharedItems();

            cacheField9.Append(sharedItems9);

            CacheField cacheField10 = new CacheField() { Name = "Task Id", NumberFormatId = (UInt32Value)0U };
            SharedItems sharedItems10 = new SharedItems();

            cacheField10.Append(sharedItems10);

            cacheFields1.Append(cacheField1);
            cacheFields1.Append(cacheField2);
            cacheFields1.Append(cacheField3);
            cacheFields1.Append(cacheField4);
            cacheFields1.Append(cacheField5);
            cacheFields1.Append(cacheField6);
            cacheFields1.Append(cacheField7);
            cacheFields1.Append(cacheField8);
            cacheFields1.Append(cacheField9);
            cacheFields1.Append(cacheField10);

            PivotCacheDefinitionExtensionList pivotCacheDefinitionExtensionList1 = new PivotCacheDefinitionExtensionList();

            PivotCacheDefinitionExtension pivotCacheDefinitionExtension1 = new PivotCacheDefinitionExtension() { Uri = "{725AE2AE-9491-48be-B2B4-4EB974FC3084}" };
            pivotCacheDefinitionExtension1.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
            X14.PivotCacheDefinition pivotCacheDefinition2 = new X14.PivotCacheDefinition();

            pivotCacheDefinitionExtension1.Append(pivotCacheDefinition2);

            pivotCacheDefinitionExtensionList1.Append(pivotCacheDefinitionExtension1);

            pivotCacheDefinition1.Append(cacheSource1);
            pivotCacheDefinition1.Append(cacheFields1);
            pivotCacheDefinition1.Append(pivotCacheDefinitionExtensionList1);

            pivotTableCacheDefinitionPart1.PivotCacheDefinition = pivotCacheDefinition1;
        }

        // Generates content of pivotTableCacheRecordsPart1.
        private void GeneratePivotTableCacheRecordsPart1Content(PivotTableCacheRecordsPart pivotTableCacheRecordsPart1)
        {
            PivotCacheRecords pivotCacheRecords1 = new PivotCacheRecords() { Count = (UInt32Value)7U };
            pivotCacheRecords1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            pivotCacheRecords1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //pivotCacheRecords1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");

            PivotCacheRecord pivotCacheRecord1 = new PivotCacheRecord();
            StringItem stringItem7 = new StringItem() { Val = "Temp control unit 3 fault" };
            StringItem stringItem8 = new StringItem() { Val = "Will Gregg" };
            StringItem stringItem9 = new StringItem() { Val = "grjoh@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem7 = new FieldItem() { Val = (UInt32Value)0U };
            FieldItem fieldItem8 = new FieldItem() { Val = (UInt32Value)0U };
            StringItem stringItem10 = new StringItem() { Val = "Tue Oct 15 2019" };
            MissingItem missingItem1 = new MissingItem();
            MissingItem missingItem2 = new MissingItem();
            StringItem stringItem11 = new StringItem() { Val = "Tue Oct 08 2019" };
            StringItem stringItem12 = new StringItem() { Val = "XsXC7qa2R0OiDwqEBjdXzGUACL9V" };

            pivotCacheRecord1.Append(stringItem7);
            pivotCacheRecord1.Append(stringItem8);
            pivotCacheRecord1.Append(stringItem9);
            pivotCacheRecord1.Append(fieldItem7);
            pivotCacheRecord1.Append(fieldItem8);
            pivotCacheRecord1.Append(stringItem10);
            pivotCacheRecord1.Append(missingItem1);
            pivotCacheRecord1.Append(missingItem2);
            pivotCacheRecord1.Append(stringItem11);
            pivotCacheRecord1.Append(stringItem12);

            PivotCacheRecord pivotCacheRecord2 = new PivotCacheRecord();
            StringItem stringItem13 = new StringItem() { Val = "Cafe espresso machine leak" };
            StringItem stringItem14 = new StringItem() { Val = "Tarun Chopra" };
            StringItem stringItem15 = new StringItem() { Val = "tarunc@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem9 = new FieldItem() { Val = (UInt32Value)1U };
            FieldItem fieldItem10 = new FieldItem() { Val = (UInt32Value)1U };
            StringItem stringItem16 = new StringItem() { Val = "Mon Oct 28 2019" };
            MissingItem missingItem3 = new MissingItem();
            MissingItem missingItem4 = new MissingItem();
            StringItem stringItem17 = new StringItem() { Val = "Mon Oct 07 2019" };
            StringItem stringItem18 = new StringItem() { Val = "m_KW-UFMKU-DPR0BmZZbW2UAKkxu" };

            pivotCacheRecord2.Append(stringItem13);
            pivotCacheRecord2.Append(stringItem14);
            pivotCacheRecord2.Append(stringItem15);
            pivotCacheRecord2.Append(fieldItem9);
            pivotCacheRecord2.Append(fieldItem10);
            pivotCacheRecord2.Append(stringItem16);
            pivotCacheRecord2.Append(missingItem3);
            pivotCacheRecord2.Append(missingItem4);
            pivotCacheRecord2.Append(stringItem17);
            pivotCacheRecord2.Append(stringItem18);

            PivotCacheRecord pivotCacheRecord3 = new PivotCacheRecord();
            StringItem stringItem19 = new StringItem() { Val = "Painting broken" };
            StringItem stringItem20 = new StringItem() { Val = "Tom Jebo" };
            StringItem stringItem21 = new StringItem() { Val = "tomjebo@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem11 = new FieldItem() { Val = (UInt32Value)2U };
            FieldItem fieldItem12 = new FieldItem() { Val = (UInt32Value)0U };
            StringItem stringItem22 = new StringItem() { Val = "Mon Oct 07 2019" };
            MissingItem missingItem5 = new MissingItem();
            MissingItem missingItem6 = new MissingItem();
            StringItem stringItem23 = new StringItem() { Val = "Sun Oct 06 2019" };
            StringItem stringItem24 = new StringItem() { Val = "KpZ1JQ7pBUK9_Vc5vkNaOGUALtPI" };

            pivotCacheRecord3.Append(stringItem19);
            pivotCacheRecord3.Append(stringItem20);
            pivotCacheRecord3.Append(stringItem21);
            pivotCacheRecord3.Append(fieldItem11);
            pivotCacheRecord3.Append(fieldItem12);
            pivotCacheRecord3.Append(stringItem22);
            pivotCacheRecord3.Append(missingItem5);
            pivotCacheRecord3.Append(missingItem6);
            pivotCacheRecord3.Append(stringItem23);
            pivotCacheRecord3.Append(stringItem24);

            PivotCacheRecord pivotCacheRecord4 = new PivotCacheRecord();
            StringItem stringItem25 = new StringItem() { Val = "Painting problem" };
            StringItem stringItem26 = new StringItem() { Val = "Tom Jebo" };
            StringItem stringItem27 = new StringItem() { Val = "tomjebo@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem13 = new FieldItem() { Val = (UInt32Value)2U };
            FieldItem fieldItem14 = new FieldItem() { Val = (UInt32Value)1U };
            StringItem stringItem28 = new StringItem() { Val = "Wed Oct 09 2019" };
            MissingItem missingItem7 = new MissingItem();
            MissingItem missingItem8 = new MissingItem();
            StringItem stringItem29 = new StringItem() { Val = "Sat Oct 05 2019" };
            StringItem stringItem30 = new StringItem() { Val = "AwFR1Ni2mEynSonFD0-3G2UAOfd2" };

            pivotCacheRecord4.Append(stringItem25);
            pivotCacheRecord4.Append(stringItem26);
            pivotCacheRecord4.Append(stringItem27);
            pivotCacheRecord4.Append(fieldItem13);
            pivotCacheRecord4.Append(fieldItem14);
            pivotCacheRecord4.Append(stringItem28);
            pivotCacheRecord4.Append(missingItem7);
            pivotCacheRecord4.Append(missingItem8);
            pivotCacheRecord4.Append(stringItem29);
            pivotCacheRecord4.Append(stringItem30);

            PivotCacheRecord pivotCacheRecord5 = new PivotCacheRecord();
            StringItem stringItem31 = new StringItem() { Val = "New issue" };
            StringItem stringItem32 = new StringItem() { Val = "Tom Jebo" };
            StringItem stringItem33 = new StringItem() { Val = "tomjebo@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem15 = new FieldItem() { Val = (UInt32Value)2U };
            FieldItem fieldItem16 = new FieldItem() { Val = (UInt32Value)1U };
            StringItem stringItem34 = new StringItem() { Val = "Fri Oct 11 2019" };
            MissingItem missingItem9 = new MissingItem();
            MissingItem missingItem10 = new MissingItem();
            StringItem stringItem35 = new StringItem() { Val = "Fri Oct 04 2019" };
            StringItem stringItem36 = new StringItem() { Val = "6W6L9MEr6kmCZtYIcYsb2mUAJfOu" };

            pivotCacheRecord5.Append(stringItem31);
            pivotCacheRecord5.Append(stringItem32);
            pivotCacheRecord5.Append(stringItem33);
            pivotCacheRecord5.Append(fieldItem15);
            pivotCacheRecord5.Append(fieldItem16);
            pivotCacheRecord5.Append(stringItem34);
            pivotCacheRecord5.Append(missingItem9);
            pivotCacheRecord5.Append(missingItem10);
            pivotCacheRecord5.Append(stringItem35);
            pivotCacheRecord5.Append(stringItem36);

            PivotCacheRecord pivotCacheRecord6 = new PivotCacheRecord();
            StringItem stringItem37 = new StringItem() { Val = "Jean Reno is cool" };
            StringItem stringItem38 = new StringItem() { Val = "Will Gregg" };
            StringItem stringItem39 = new StringItem() { Val = "grjoh@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem17 = new FieldItem() { Val = (UInt32Value)2U };
            FieldItem fieldItem18 = new FieldItem() { Val = (UInt32Value)0U };
            StringItem stringItem40 = new StringItem() { Val = "Wed Oct 09 2019" };
            MissingItem missingItem11 = new MissingItem();
            MissingItem missingItem12 = new MissingItem();
            StringItem stringItem41 = new StringItem() { Val = "Thu Oct 03 2019" };
            StringItem stringItem42 = new StringItem() { Val = "rabpSxRAbES1Mp6Ysl1c4WUAIxzx" };

            pivotCacheRecord6.Append(stringItem37);
            pivotCacheRecord6.Append(stringItem38);
            pivotCacheRecord6.Append(stringItem39);
            pivotCacheRecord6.Append(fieldItem17);
            pivotCacheRecord6.Append(fieldItem18);
            pivotCacheRecord6.Append(stringItem40);
            pivotCacheRecord6.Append(missingItem11);
            pivotCacheRecord6.Append(missingItem12);
            pivotCacheRecord6.Append(stringItem41);
            pivotCacheRecord6.Append(stringItem42);

            PivotCacheRecord pivotCacheRecord7 = new PivotCacheRecord();
            StringItem stringItem43 = new StringItem() { Val = "McDonald\'s deep frier repair" };
            StringItem stringItem44 = new StringItem() { Val = "Will Gregg" };
            StringItem stringItem45 = new StringItem() { Val = "grjoh@jebosoft.onmicrosoft.com" };
            FieldItem fieldItem19 = new FieldItem() { Val = (UInt32Value)1U };
            FieldItem fieldItem20 = new FieldItem() { Val = (UInt32Value)2U };
            StringItem stringItem46 = new StringItem() { Val = "Wed Oct 09 2019" };
            StringItem stringItem47 = new StringItem() { Val = "Mon Oct 07 2019" };
            MissingItem missingItem13 = new MissingItem();
            StringItem stringItem48 = new StringItem() { Val = "Mon Oct 07 2019" };
            StringItem stringItem49 = new StringItem() { Val = "Test Inline Str" };

            pivotCacheRecord7.Append(stringItem43);
            pivotCacheRecord7.Append(stringItem44);
            pivotCacheRecord7.Append(stringItem45);
            pivotCacheRecord7.Append(fieldItem19);
            pivotCacheRecord7.Append(fieldItem20);
            pivotCacheRecord7.Append(stringItem46);
            pivotCacheRecord7.Append(stringItem47);
            pivotCacheRecord7.Append(missingItem13);
            pivotCacheRecord7.Append(stringItem48);
            pivotCacheRecord7.Append(stringItem49);

            pivotCacheRecords1.Append(pivotCacheRecord1);
            pivotCacheRecords1.Append(pivotCacheRecord2);
            pivotCacheRecords1.Append(pivotCacheRecord3);
            pivotCacheRecords1.Append(pivotCacheRecord4);
            pivotCacheRecords1.Append(pivotCacheRecord5);
            pivotCacheRecords1.Append(pivotCacheRecord6);
            pivotCacheRecords1.Append(pivotCacheRecord7);

            pivotTableCacheRecordsPart1.PivotCacheRecords = pivotCacheRecords1;
        }

        // Generates content of pivotTablePart2.
        private void GeneratePivotTablePart2Content(PivotTablePart pivotTablePart2)
        {
            PivotTableDefinition pivotTableDefinition3 = new PivotTableDefinition() { Name = "PivotTable23", CacheId = (UInt32Value)4U, ApplyNumberFormats = false, ApplyBorderFormats = false, ApplyFontFormats = false, ApplyPatternFormats = false, ApplyAlignmentFormats = false, ApplyWidthHeightFormats = true, DataCaption = "Values", UpdatedVersion = 6, MinRefreshableVersion = 3, UseAutoFormatting = true, ItemPrintTitles = true, CreatedVersion = 6, Indent = (UInt32Value)0U, Outline = true, OutlineData = true, MultipleFieldFilters = false, ChartFormat = (UInt32Value)10U };
            pivotTableDefinition3.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //pivotTableDefinition3.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //pivotTableDefinition3.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{AA5567E3-EB0A-49B7-8083-FE15594B9370}"));
            DocumentFormat.OpenXml.Spreadsheet.Location location2 = new DocumentFormat.OpenXml.Spreadsheet.Location() { Reference = "H34:I38", FirstHeaderRow = (UInt32Value)1U, FirstDataRow = (UInt32Value)1U, FirstDataColumn = (UInt32Value)1U };

            PivotFields pivotFields2 = new PivotFields() { Count = (UInt32Value)10U };
            PivotField pivotField11 = new PivotField() { DataField = true, ShowAll = false };
            PivotField pivotField12 = new PivotField() { ShowAll = false };
            PivotField pivotField13 = new PivotField() { ShowAll = false };
            PivotField pivotField14 = new PivotField() { ShowAll = false };

            PivotField pivotField15 = new PivotField() { Axis = PivotTableAxisValues.AxisRow, MultipleItemSelectionAllowed = true, ShowAll = false };

            Items items3 = new Items() { Count = (UInt32Value)4U };
            Item item9 = new Item() { Index = (UInt32Value)2U };
            Item item10 = new Item() { Index = (UInt32Value)1U };
            Item item11 = new Item() { Index = (UInt32Value)0U };
            Item item12 = new Item() { ItemType = ItemValues.Default };

            items3.Append(item9);
            items3.Append(item10);
            items3.Append(item11);
            items3.Append(item12);

            pivotField15.Append(items3);
            PivotField pivotField16 = new PivotField() { MultipleItemSelectionAllowed = true, ShowAll = false, DefaultSubtotal = false };
            PivotField pivotField17 = new PivotField() { ShowAll = false };
            PivotField pivotField18 = new PivotField() { ShowAll = false };
            PivotField pivotField19 = new PivotField() { ShowAll = false };
            PivotField pivotField20 = new PivotField() { ShowAll = false };

            pivotFields2.Append(pivotField11);
            pivotFields2.Append(pivotField12);
            pivotFields2.Append(pivotField13);
            pivotFields2.Append(pivotField14);
            pivotFields2.Append(pivotField15);
            pivotFields2.Append(pivotField16);
            pivotFields2.Append(pivotField17);
            pivotFields2.Append(pivotField18);
            pivotFields2.Append(pivotField19);
            pivotFields2.Append(pivotField20);

            RowFields rowFields2 = new RowFields() { Count = (UInt32Value)1U };
            Field field3 = new Field() { Index = 4 };

            rowFields2.Append(field3);

            RowItems rowItems2 = new RowItems() { Count = (UInt32Value)4U };

            RowItem rowItem9 = new RowItem();
            MemberPropertyIndex memberPropertyIndex9 = new MemberPropertyIndex();

            rowItem9.Append(memberPropertyIndex9);

            RowItem rowItem10 = new RowItem();
            MemberPropertyIndex memberPropertyIndex10 = new MemberPropertyIndex() { Val = 1 };

            rowItem10.Append(memberPropertyIndex10);

            RowItem rowItem11 = new RowItem();
            MemberPropertyIndex memberPropertyIndex11 = new MemberPropertyIndex() { Val = 2 };

            rowItem11.Append(memberPropertyIndex11);

            RowItem rowItem12 = new RowItem() { ItemType = ItemValues.Grand };
            MemberPropertyIndex memberPropertyIndex12 = new MemberPropertyIndex();

            rowItem12.Append(memberPropertyIndex12);

            rowItems2.Append(rowItem9);
            rowItems2.Append(rowItem10);
            rowItems2.Append(rowItem11);
            rowItems2.Append(rowItem12);

            ColumnItems columnItems2 = new ColumnItems() { Count = (UInt32Value)1U };
            RowItem rowItem13 = new RowItem();

            columnItems2.Append(rowItem13);

            DataFields dataFields2 = new DataFields() { Count = (UInt32Value)1U };
            DataField dataField2 = new DataField() { Name = "Count of Task", Field = (UInt32Value)0U, Subtotal = DataConsolidateFunctionValues.Count, BaseField = 0, BaseItem = (UInt32Value)0U };

            dataFields2.Append(dataField2);

            ChartFormats chartFormats2 = new ChartFormats() { Count = (UInt32Value)4U };

            ChartFormat chartFormat4 = new ChartFormat() { Chart = (UInt32Value)9U, Format = (UInt32Value)0U, Series = true };

            PivotArea pivotArea4 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences4 = new PivotAreaReferences() { Count = (UInt32Value)1U };

            PivotAreaReference pivotAreaReference7 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem21 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference7.Append(fieldItem21);

            pivotAreaReferences4.Append(pivotAreaReference7);

            pivotArea4.Append(pivotAreaReferences4);

            chartFormat4.Append(pivotArea4);

            ChartFormat chartFormat5 = new ChartFormat() { Chart = (UInt32Value)9U, Format = (UInt32Value)1U };

            PivotArea pivotArea5 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences5 = new PivotAreaReferences() { Count = (UInt32Value)2U };

            PivotAreaReference pivotAreaReference8 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem22 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference8.Append(fieldItem22);

            PivotAreaReference pivotAreaReference9 = new PivotAreaReference() { Field = (UInt32Value)4U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem23 = new FieldItem() { Val = (UInt32Value)1U };

            pivotAreaReference9.Append(fieldItem23);

            pivotAreaReferences5.Append(pivotAreaReference8);
            pivotAreaReferences5.Append(pivotAreaReference9);

            pivotArea5.Append(pivotAreaReferences5);

            chartFormat5.Append(pivotArea5);

            ChartFormat chartFormat6 = new ChartFormat() { Chart = (UInt32Value)9U, Format = (UInt32Value)2U };

            PivotArea pivotArea6 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences6 = new PivotAreaReferences() { Count = (UInt32Value)2U };

            PivotAreaReference pivotAreaReference10 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem24 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference10.Append(fieldItem24);

            PivotAreaReference pivotAreaReference11 = new PivotAreaReference() { Field = (UInt32Value)4U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem25 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference11.Append(fieldItem25);

            pivotAreaReferences6.Append(pivotAreaReference10);
            pivotAreaReferences6.Append(pivotAreaReference11);

            pivotArea6.Append(pivotAreaReferences6);

            chartFormat6.Append(pivotArea6);

            ChartFormat chartFormat7 = new ChartFormat() { Chart = (UInt32Value)9U, Format = (UInt32Value)3U };

            PivotArea pivotArea7 = new PivotArea() { Type = PivotAreaValues.Data, Outline = false, FieldPosition = (UInt32Value)0U };

            PivotAreaReferences pivotAreaReferences7 = new PivotAreaReferences() { Count = (UInt32Value)2U };

            PivotAreaReference pivotAreaReference12 = new PivotAreaReference() { Field = (UInt32Value)4294967294U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem26 = new FieldItem() { Val = (UInt32Value)0U };

            pivotAreaReference12.Append(fieldItem26);

            PivotAreaReference pivotAreaReference13 = new PivotAreaReference() { Field = (UInt32Value)4U, Count = (UInt32Value)1U, Selected = false };
            FieldItem fieldItem27 = new FieldItem() { Val = (UInt32Value)2U };

            pivotAreaReference13.Append(fieldItem27);

            pivotAreaReferences7.Append(pivotAreaReference12);
            pivotAreaReferences7.Append(pivotAreaReference13);

            pivotArea7.Append(pivotAreaReferences7);

            chartFormat7.Append(pivotArea7);

            chartFormats2.Append(chartFormat4);
            chartFormats2.Append(chartFormat5);
            chartFormats2.Append(chartFormat6);
            chartFormats2.Append(chartFormat7);
            PivotTableStyle pivotTableStyle2 = new PivotTableStyle() { Name = "PivotStyleMedium9", ShowRowHeaders = true, ShowColumnHeaders = true, ShowRowStripes = false, ShowColumnStripes = false, ShowLastColumn = true };

            PivotTableDefinitionExtensionList pivotTableDefinitionExtensionList2 = new PivotTableDefinitionExtensionList();

            PivotTableDefinitionExtension pivotTableDefinitionExtension3 = new PivotTableDefinitionExtension() { Uri = "{962EF5D1-5CA2-4c93-8EF4-DBF5C05439D2}" };
            pivotTableDefinitionExtension3.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");

            X14.PivotTableDefinition pivotTableDefinition4 = new X14.PivotTableDefinition() { HideValuesRow = true };
            pivotTableDefinition4.AddNamespaceDeclaration("xm", "http://schemas.microsoft.com/office/excel/2006/main");

            pivotTableDefinitionExtension3.Append(pivotTableDefinition4);

            PivotTableDefinitionExtension pivotTableDefinitionExtension4 = new PivotTableDefinitionExtension() { Uri = "{747A6164-185A-40DC-8AA5-F01512510D54}" };
            pivotTableDefinitionExtension4.AddNamespaceDeclaration("xpdl", "http://schemas.microsoft.com/office/spreadsheetml/2016/pivotdefaultlayout");
            OpenXmlUnknownElement openXmlUnknownElement5 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<xpdl:pivotTableDefinition16 xmlns:xpdl=\"http://schemas.microsoft.com/office/spreadsheetml/2016/pivotdefaultlayout\" />");

            pivotTableDefinitionExtension4.Append(openXmlUnknownElement5);

            pivotTableDefinitionExtensionList2.Append(pivotTableDefinitionExtension3);
            pivotTableDefinitionExtensionList2.Append(pivotTableDefinitionExtension4);

            pivotTableDefinition3.Append(location2);
            pivotTableDefinition3.Append(pivotFields2);
            pivotTableDefinition3.Append(rowFields2);
            pivotTableDefinition3.Append(rowItems2);
            pivotTableDefinition3.Append(columnItems2);
            pivotTableDefinition3.Append(dataFields2);
            pivotTableDefinition3.Append(chartFormats2);
            pivotTableDefinition3.Append(pivotTableStyle2);
            pivotTableDefinition3.Append(pivotTableDefinitionExtensionList2);

            pivotTablePart2.PivotTableDefinition = pivotTableDefinition3;
        }

        // Generates content of drawingsPart1.
        private void GenerateDrawingsPart1Content(DrawingsPart drawingsPart1)
        {
            Xdr.WorksheetDrawing worksheetDrawing1 = new Xdr.WorksheetDrawing();
            worksheetDrawing1.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
            worksheetDrawing1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = "0";
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = "519112";
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = "13";
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = "38100";

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = "5";
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = "366712";
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = "27";
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = "114300";

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.GraphicFrame graphicFrame1 = new Xdr.GraphicFrame() { Macro = "" };

            Xdr.NonVisualGraphicFrameProperties nonVisualGraphicFrameProperties1 = new Xdr.NonVisualGraphicFrameProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "Chart 1" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList1 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension1 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement6 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{5E79015C-1433-4854-B048-179923F8BEE9}\" />");

            nonVisualDrawingPropertiesExtension1.Append(openXmlUnknownElement6);

            nonVisualDrawingPropertiesExtensionList1.Append(nonVisualDrawingPropertiesExtension1);

            nonVisualDrawingProperties1.Append(nonVisualDrawingPropertiesExtensionList1);
            Xdr.NonVisualGraphicFrameDrawingProperties nonVisualGraphicFrameDrawingProperties1 = new Xdr.NonVisualGraphicFrameDrawingProperties();

            nonVisualGraphicFrameProperties1.Append(nonVisualDrawingProperties1);
            nonVisualGraphicFrameProperties1.Append(nonVisualGraphicFrameDrawingProperties1);

            Xdr.Transform transform1 = new Xdr.Transform();
            A.Offset offset1 = new A.Offset() { X = 0L, Y = 0L };
            A.Extents extents1 = new A.Extents() { Cx = 0L, Cy = 0L };

            transform1.Append(offset1);
            transform1.Append(extents1);

            A.Graphic graphic1 = new A.Graphic();

            A.GraphicData graphicData1 = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" };

            C.ChartReference chartReference1 = new C.ChartReference() { Id = "rId1" };
            chartReference1.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
            chartReference1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            graphicData1.Append(chartReference1);

            graphic1.Append(graphicData1);

            graphicFrame1.Append(nonVisualGraphicFrameProperties1);
            graphicFrame1.Append(transform1);
            graphicFrame1.Append(graphic1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(graphicFrame1);
            twoCellAnchor1.Append(clientData1);

            Xdr.TwoCellAnchor twoCellAnchor2 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker2 = new Xdr.FromMarker();
            Xdr.ColumnId columnId3 = new Xdr.ColumnId();
            columnId3.Text = "6";
            Xdr.ColumnOffset columnOffset3 = new Xdr.ColumnOffset();
            columnOffset3.Text = "228600";
            Xdr.RowId rowId3 = new Xdr.RowId();
            rowId3.Text = "13";
            Xdr.RowOffset rowOffset3 = new Xdr.RowOffset();
            rowOffset3.Text = "57150";

            fromMarker2.Append(columnId3);
            fromMarker2.Append(columnOffset3);
            fromMarker2.Append(rowId3);
            fromMarker2.Append(rowOffset3);

            Xdr.ToMarker toMarker2 = new Xdr.ToMarker();
            Xdr.ColumnId columnId4 = new Xdr.ColumnId();
            columnId4.Text = "11";
            Xdr.ColumnOffset columnOffset4 = new Xdr.ColumnOffset();
            columnOffset4.Text = "704850";
            Xdr.RowId rowId4 = new Xdr.RowId();
            rowId4.Text = "27";
            Xdr.RowOffset rowOffset4 = new Xdr.RowOffset();
            rowOffset4.Text = "133350";

            toMarker2.Append(columnId4);
            toMarker2.Append(columnOffset4);
            toMarker2.Append(rowId4);
            toMarker2.Append(rowOffset4);

            Xdr.GraphicFrame graphicFrame2 = new Xdr.GraphicFrame() { Macro = "" };

            Xdr.NonVisualGraphicFrameProperties nonVisualGraphicFrameProperties2 = new Xdr.NonVisualGraphicFrameProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties2 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)3U, Name = "Chart 2" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList2 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension2 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement7 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{71C390B4-509C-43A4-BD69-AC638451F404}\" />");

            nonVisualDrawingPropertiesExtension2.Append(openXmlUnknownElement7);

            nonVisualDrawingPropertiesExtensionList2.Append(nonVisualDrawingPropertiesExtension2);

            nonVisualDrawingProperties2.Append(nonVisualDrawingPropertiesExtensionList2);
            Xdr.NonVisualGraphicFrameDrawingProperties nonVisualGraphicFrameDrawingProperties2 = new Xdr.NonVisualGraphicFrameDrawingProperties();

            nonVisualGraphicFrameProperties2.Append(nonVisualDrawingProperties2);
            nonVisualGraphicFrameProperties2.Append(nonVisualGraphicFrameDrawingProperties2);

            Xdr.Transform transform2 = new Xdr.Transform();
            A.Offset offset2 = new A.Offset() { X = 0L, Y = 0L };
            A.Extents extents2 = new A.Extents() { Cx = 0L, Cy = 0L };

            transform2.Append(offset2);
            transform2.Append(extents2);

            A.Graphic graphic2 = new A.Graphic();

            A.GraphicData graphicData2 = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" };

            C.ChartReference chartReference2 = new C.ChartReference() { Id = "rId2" };
            chartReference2.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
            chartReference2.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            graphicData2.Append(chartReference2);

            graphic2.Append(graphicData2);

            graphicFrame2.Append(nonVisualGraphicFrameProperties2);
            graphicFrame2.Append(transform2);
            graphicFrame2.Append(graphic2);
            Xdr.ClientData clientData2 = new Xdr.ClientData();

            twoCellAnchor2.Append(fromMarker2);
            twoCellAnchor2.Append(toMarker2);
            twoCellAnchor2.Append(graphicFrame2);
            twoCellAnchor2.Append(clientData2);

            Xdr.TwoCellAnchor twoCellAnchor3 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker3 = new Xdr.FromMarker();
            Xdr.ColumnId columnId5 = new Xdr.ColumnId();
            columnId5.Text = "0";
            Xdr.ColumnOffset columnOffset5 = new Xdr.ColumnOffset();
            columnOffset5.Text = "514350";
            Xdr.RowId rowId5 = new Xdr.RowId();
            rowId5.Text = "1";
            Xdr.RowOffset rowOffset5 = new Xdr.RowOffset();
            rowOffset5.Text = "152400";

            fromMarker3.Append(columnId5);
            fromMarker3.Append(columnOffset5);
            fromMarker3.Append(rowId5);
            fromMarker3.Append(rowOffset5);

            Xdr.ToMarker toMarker3 = new Xdr.ToMarker();
            Xdr.ColumnId columnId6 = new Xdr.ColumnId();
            columnId6.Text = "3";
            Xdr.ColumnOffset columnOffset6 = new Xdr.ColumnOffset();
            columnOffset6.Text = "390526";
            Xdr.RowId rowId6 = new Xdr.RowId();
            rowId6.Text = "4";
            Xdr.RowOffset rowOffset6 = new Xdr.RowOffset();
            rowOffset6.Text = "38100";

            toMarker3.Append(columnId6);
            toMarker3.Append(columnOffset6);
            toMarker3.Append(rowId6);
            toMarker3.Append(rowOffset6);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)4U, Name = "TextBox 3" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList3 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension3 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement8 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{D6E05017-EE1F-4927-8E1A-EBB00EB149E6}\" />");

            nonVisualDrawingPropertiesExtension3.Append(openXmlUnknownElement8);

            nonVisualDrawingPropertiesExtensionList3.Append(nonVisualDrawingPropertiesExtension3);

            nonVisualDrawingProperties3.Append(nonVisualDrawingPropertiesExtensionList3);
            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties();

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset3 = new A.Offset() { X = 514350L, Y = 342900L };
            A.Extents extents3 = new A.Extents() { Cx = 3114676L, Cy = 457200L };

            transform2D1.Append(offset3);
            transform2D1.Append(extents3);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill1 = new A.SolidFill();
            A.SchemeColor schemeColor1 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill1.Append(schemeColor1);

            A.Outline outline1 = new A.Outline() { Width = 9525, CompoundLineType = A.CompoundLineValues.Single };

            A.SolidFill solidFill2 = new A.SolidFill();

            A.SchemeColor schemeColor2 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };
            A.Shade shade1 = new A.Shade() { Val = 50000 };

            schemeColor2.Append(shade1);

            solidFill2.Append(schemeColor2);

            outline1.Append(solidFill2);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill1);
            shapeProperties1.Append(outline1);

            Xdr.ShapeStyle shapeStyle1 = new Xdr.ShapeStyle();

            A.LineReference lineReference1 = new A.LineReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage1 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            lineReference1.Append(rgbColorModelPercentage1);

            A.FillReference fillReference1 = new A.FillReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage2 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            fillReference1.Append(rgbColorModelPercentage2);

            A.EffectReference effectReference1 = new A.EffectReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage3 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            effectReference1.Append(rgbColorModelPercentage3);

            A.FontReference fontReference1 = new A.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor3 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };

            fontReference1.Append(schemeColor3);

            shapeStyle1.Append(lineReference1);
            shapeStyle1.Append(fillReference1);
            shapeStyle1.Append(effectReference1);
            shapeStyle1.Append(fontReference1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();
            A.BodyProperties bodyProperties1 = new A.BodyProperties() { VerticalOverflow = A.TextVerticalOverflowValues.Clip, HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, Wrap = A.TextWrappingValues.Square, RightToLeftColumns = false, Anchor = A.TextAnchoringTypeValues.Top };
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.Run run1 = new A.Run();
            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", FontSize = 1800 };
            A.Text text55 = new A.Text();
            text55.Text = "Focus Report for Focus";

            run1.Append(runProperties1);
            run1.Append(text55);

            A.Run run2 = new A.Run();
            A.RunProperties runProperties2 = new A.RunProperties() { Language = "en-US", FontSize = 1800, Baseline = 0 };
            A.Text text56 = new A.Text();
            text56.Text = " Bucket";

            run2.Append(runProperties2);
            run2.Append(text56);
            A.EndParagraphRunProperties endParagraphRunProperties1 = new A.EndParagraphRunProperties() { Language = "en-US", FontSize = 1800 };

            paragraph1.Append(run1);
            paragraph1.Append(run2);
            paragraph1.Append(endParagraphRunProperties1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(shapeStyle1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData3 = new Xdr.ClientData();

            twoCellAnchor3.Append(fromMarker3);
            twoCellAnchor3.Append(toMarker3);
            twoCellAnchor3.Append(shape1);
            twoCellAnchor3.Append(clientData3);

            Xdr.TwoCellAnchor twoCellAnchor4 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker4 = new Xdr.FromMarker();
            Xdr.ColumnId columnId7 = new Xdr.ColumnId();
            columnId7.Text = "0";
            Xdr.ColumnOffset columnOffset7 = new Xdr.ColumnOffset();
            columnOffset7.Text = "495300";
            Xdr.RowId rowId7 = new Xdr.RowId();
            rowId7.Text = "4";
            Xdr.RowOffset rowOffset7 = new Xdr.RowOffset();
            rowOffset7.Text = "95250";

            fromMarker4.Append(columnId7);
            fromMarker4.Append(columnOffset7);
            fromMarker4.Append(rowId7);
            fromMarker4.Append(rowOffset7);

            Xdr.ToMarker toMarker4 = new Xdr.ToMarker();
            Xdr.ColumnId columnId8 = new Xdr.ColumnId();
            columnId8.Text = "1";
            Xdr.ColumnOffset columnOffset8 = new Xdr.ColumnOffset();
            columnOffset8.Text = "942975";
            Xdr.RowId rowId8 = new Xdr.RowId();
            rowId8.Text = "6";
            Xdr.RowOffset rowOffset8 = new Xdr.RowOffset();
            rowOffset8.Text = "171450";

            toMarker4.Append(columnId8);
            toMarker4.Append(columnOffset8);
            toMarker4.Append(rowId8);
            toMarker4.Append(rowOffset8);

            Xdr.Shape shape2 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties2 = new Xdr.NonVisualShapeProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties4 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)5U, Name = "TextBox 4" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList4 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension4 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement9 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{8B0838B4-7F61-47ED-BDEC-9B0B7DBBFE58}\" />");

            nonVisualDrawingPropertiesExtension4.Append(openXmlUnknownElement9);

            nonVisualDrawingPropertiesExtensionList4.Append(nonVisualDrawingPropertiesExtension4);

            nonVisualDrawingProperties4.Append(nonVisualDrawingPropertiesExtensionList4);
            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties2 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };

            nonVisualShapeProperties2.Append(nonVisualDrawingProperties4);
            nonVisualShapeProperties2.Append(nonVisualShapeDrawingProperties2);

            Xdr.ShapeProperties shapeProperties2 = new Xdr.ShapeProperties();

            A.Transform2D transform2D2 = new A.Transform2D();
            A.Offset offset4 = new A.Offset() { X = 495300L, Y = 857250L };
            A.Extents extents4 = new A.Extents() { Cx = 1885950L, Cy = 457200L };

            transform2D2.Append(offset4);
            transform2D2.Append(extents4);

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList2 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList2);

            A.SolidFill solidFill3 = new A.SolidFill();
            A.SchemeColor schemeColor4 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill3.Append(schemeColor4);

            A.Outline outline2 = new A.Outline() { Width = 9525, CompoundLineType = A.CompoundLineValues.Single };

            A.SolidFill solidFill4 = new A.SolidFill();

            A.SchemeColor schemeColor5 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };
            A.Shade shade2 = new A.Shade() { Val = 50000 };

            schemeColor5.Append(shade2);

            solidFill4.Append(schemeColor5);

            outline2.Append(solidFill4);

            shapeProperties2.Append(transform2D2);
            shapeProperties2.Append(presetGeometry2);
            shapeProperties2.Append(solidFill3);
            shapeProperties2.Append(outline2);

            Xdr.ShapeStyle shapeStyle2 = new Xdr.ShapeStyle();

            A.LineReference lineReference2 = new A.LineReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage4 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            lineReference2.Append(rgbColorModelPercentage4);

            A.FillReference fillReference2 = new A.FillReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage5 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            fillReference2.Append(rgbColorModelPercentage5);

            A.EffectReference effectReference2 = new A.EffectReference() { Index = (UInt32Value)0U };
            A.RgbColorModelPercentage rgbColorModelPercentage6 = new A.RgbColorModelPercentage() { RedPortion = 0, GreenPortion = 0, BluePortion = 0 };

            effectReference2.Append(rgbColorModelPercentage6);

            A.FontReference fontReference2 = new A.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor6 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };

            fontReference2.Append(schemeColor6);

            shapeStyle2.Append(lineReference2);
            shapeStyle2.Append(fillReference2);
            shapeStyle2.Append(effectReference2);
            shapeStyle2.Append(fontReference2);

            Xdr.TextBody textBody2 = new Xdr.TextBody();
            A.BodyProperties bodyProperties2 = new A.BodyProperties() { VerticalOverflow = A.TextVerticalOverflowValues.Clip, HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, Wrap = A.TextWrappingValues.Square, RightToLeftColumns = false, Anchor = A.TextAnchoringTypeValues.Top };
            A.ListStyle listStyle2 = new A.ListStyle();

            A.Paragraph paragraph2 = new A.Paragraph();

            A.Run run3 = new A.Run();
            A.RunProperties runProperties3 = new A.RunProperties() { Language = "en-US", FontSize = 1800 };
            A.Text text57 = new A.Text();
            text57.Text = "October 8, 2019";

            run3.Append(runProperties3);
            run3.Append(text57);

            paragraph2.Append(run3);

            textBody2.Append(bodyProperties2);
            textBody2.Append(listStyle2);
            textBody2.Append(paragraph2);

            shape2.Append(nonVisualShapeProperties2);
            shape2.Append(shapeProperties2);
            shape2.Append(shapeStyle2);
            shape2.Append(textBody2);
            Xdr.ClientData clientData4 = new Xdr.ClientData();

            twoCellAnchor4.Append(fromMarker4);
            twoCellAnchor4.Append(toMarker4);
            twoCellAnchor4.Append(shape2);
            twoCellAnchor4.Append(clientData4);

            Xdr.TwoCellAnchor twoCellAnchor5 = new Xdr.TwoCellAnchor() { EditAs = Xdr.EditAsValues.OneCell };

            Xdr.FromMarker fromMarker5 = new Xdr.FromMarker();
            Xdr.ColumnId columnId9 = new Xdr.ColumnId();
            columnId9.Text = "6";
            Xdr.ColumnOffset columnOffset9 = new Xdr.ColumnOffset();
            columnOffset9.Text = "571500";
            Xdr.RowId rowId9 = new Xdr.RowId();
            rowId9.Text = "1";
            Xdr.RowOffset rowOffset9 = new Xdr.RowOffset();
            rowOffset9.Text = "133350";

            fromMarker5.Append(columnId9);
            fromMarker5.Append(columnOffset9);
            fromMarker5.Append(rowId9);
            fromMarker5.Append(rowOffset9);

            Xdr.ToMarker toMarker5 = new Xdr.ToMarker();
            Xdr.ColumnId columnId10 = new Xdr.ColumnId();
            columnId10.Text = "9";
            Xdr.ColumnOffset columnOffset10 = new Xdr.ColumnOffset();
            columnOffset10.Text = "419100";
            Xdr.RowId rowId10 = new Xdr.RowId();
            rowId10.Text = "11";
            Xdr.RowOffset rowOffset10 = new Xdr.RowOffset();
            rowOffset10.Text = "95250";

            toMarker5.Append(columnId10);
            toMarker5.Append(columnOffset10);
            toMarker5.Append(rowId10);
            toMarker5.Append(rowOffset10);

            Xdr.Picture picture1 = new Xdr.Picture();

            Xdr.NonVisualPictureProperties nonVisualPictureProperties1 = new Xdr.NonVisualPictureProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties5 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)7U, Name = "Picture 6" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList5 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension5 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement10 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{58903F66-BAC7-4B6C-8A4A-C10CD0B6502F}\" />");

            nonVisualDrawingPropertiesExtension5.Append(openXmlUnknownElement10);

            nonVisualDrawingPropertiesExtensionList5.Append(nonVisualDrawingPropertiesExtension5);

            nonVisualDrawingProperties5.Append(nonVisualDrawingPropertiesExtensionList5);

            Xdr.NonVisualPictureDrawingProperties nonVisualPictureDrawingProperties1 = new Xdr.NonVisualPictureDrawingProperties();
            A.PictureLocks pictureLocks1 = new A.PictureLocks() { NoChangeAspect = true };

            nonVisualPictureDrawingProperties1.Append(pictureLocks1);

            nonVisualPictureProperties1.Append(nonVisualDrawingProperties5);
            nonVisualPictureProperties1.Append(nonVisualPictureDrawingProperties1);

            Xdr.BlipFill blipFill1 = new Xdr.BlipFill();

            A.Blip blip1 = new A.Blip() { Embed = "rId3" };
            blip1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            A.BlipExtensionList blipExtensionList1 = new A.BlipExtensionList();

            A.BlipExtension blipExtension1 = new A.BlipExtension() { Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}" };

            A14.UseLocalDpi useLocalDpi1 = new A14.UseLocalDpi() { Val = false };
            useLocalDpi1.AddNamespaceDeclaration("a14", "http://schemas.microsoft.com/office/drawing/2010/main");

            blipExtension1.Append(useLocalDpi1);

            blipExtensionList1.Append(blipExtension1);

            blip1.Append(blipExtensionList1);

            A.Stretch stretch1 = new A.Stretch();
            A.FillRectangle fillRectangle1 = new A.FillRectangle();

            stretch1.Append(fillRectangle1);

            blipFill1.Append(blip1);
            blipFill1.Append(stretch1);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties();

            A.Transform2D transform2D3 = new A.Transform2D();
            A.Offset offset5 = new A.Offset() { X = 5905500L, Y = 323850L };
            A.Extents extents5 = new A.Extents() { Cx = 2190750L, Cy = 1866900L };

            transform2D3.Append(offset5);
            transform2D3.Append(extents5);

            A.PresetGeometry presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);

            picture1.Append(nonVisualPictureProperties1);
            picture1.Append(blipFill1);
            picture1.Append(shapeProperties3);
            Xdr.ClientData clientData5 = new Xdr.ClientData();

            twoCellAnchor5.Append(fromMarker5);
            twoCellAnchor5.Append(toMarker5);
            twoCellAnchor5.Append(picture1);
            twoCellAnchor5.Append(clientData5);

            Xdr.TwoCellAnchor twoCellAnchor6 = new Xdr.TwoCellAnchor() { EditAs = Xdr.EditAsValues.OneCell };

            Xdr.FromMarker fromMarker6 = new Xdr.FromMarker();
            Xdr.ColumnId columnId11 = new Xdr.ColumnId();
            columnId11.Text = "13";
            Xdr.ColumnOffset columnOffset11 = new Xdr.ColumnOffset();
            columnOffset11.Text = "238125";
            Xdr.RowId rowId11 = new Xdr.RowId();
            rowId11.Text = "0";
            Xdr.RowOffset rowOffset11 = new Xdr.RowOffset();
            rowOffset11.Text = "57150";

            fromMarker6.Append(columnId11);
            fromMarker6.Append(columnOffset11);
            fromMarker6.Append(rowId11);
            fromMarker6.Append(rowOffset11);

            Xdr.ToMarker toMarker6 = new Xdr.ToMarker();
            Xdr.ColumnId columnId12 = new Xdr.ColumnId();
            columnId12.Text = "28";
            Xdr.ColumnOffset columnOffset12 = new Xdr.ColumnOffset();
            columnOffset12.Text = "76200";
            Xdr.RowId rowId12 = new Xdr.RowId();
            rowId12.Text = "30";
            Xdr.RowOffset rowOffset12 = new Xdr.RowOffset();
            rowOffset12.Text = "0";

            toMarker6.Append(columnId12);
            toMarker6.Append(columnOffset12);
            toMarker6.Append(rowId12);
            toMarker6.Append(rowOffset12);

            Xdr.Picture picture2 = new Xdr.Picture();

            Xdr.NonVisualPictureProperties nonVisualPictureProperties2 = new Xdr.NonVisualPictureProperties();

            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties6 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)9U, Name = "Picture 8" };

            A.NonVisualDrawingPropertiesExtensionList nonVisualDrawingPropertiesExtensionList6 = new A.NonVisualDrawingPropertiesExtensionList();

            A.NonVisualDrawingPropertiesExtension nonVisualDrawingPropertiesExtension6 = new A.NonVisualDrawingPropertiesExtension() { Uri = "{FF2B5EF4-FFF2-40B4-BE49-F238E27FC236}" };

            OpenXmlUnknownElement openXmlUnknownElement11 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<a16:creationId xmlns:a16=\"http://schemas.microsoft.com/office/drawing/2014/main\" id=\"{89F921A3-8E18-4FF1-A28F-919328831693}\" />");

            nonVisualDrawingPropertiesExtension6.Append(openXmlUnknownElement11);

            nonVisualDrawingPropertiesExtensionList6.Append(nonVisualDrawingPropertiesExtension6);

            nonVisualDrawingProperties6.Append(nonVisualDrawingPropertiesExtensionList6);

            Xdr.NonVisualPictureDrawingProperties nonVisualPictureDrawingProperties2 = new Xdr.NonVisualPictureDrawingProperties();
            A.PictureLocks pictureLocks2 = new A.PictureLocks() { NoChangeAspect = true };

            nonVisualPictureDrawingProperties2.Append(pictureLocks2);

            nonVisualPictureProperties2.Append(nonVisualDrawingProperties6);
            nonVisualPictureProperties2.Append(nonVisualPictureDrawingProperties2);

            Xdr.BlipFill blipFill2 = new Xdr.BlipFill();

            A.Blip blip2 = new A.Blip() { Embed = "rId4" };
            blip2.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            A.BlipExtensionList blipExtensionList2 = new A.BlipExtensionList();

            A.BlipExtension blipExtension2 = new A.BlipExtension() { Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}" };

            A14.UseLocalDpi useLocalDpi2 = new A14.UseLocalDpi() { Val = false };
            useLocalDpi2.AddNamespaceDeclaration("a14", "http://schemas.microsoft.com/office/drawing/2010/main");

            blipExtension2.Append(useLocalDpi2);

            blipExtensionList2.Append(blipExtension2);

            blip2.Append(blipExtensionList2);

            A.Stretch stretch2 = new A.Stretch();
            A.FillRectangle fillRectangle2 = new A.FillRectangle();

            stretch2.Append(fillRectangle2);

            blipFill2.Append(blip2);
            blipFill2.Append(stretch2);

            Xdr.ShapeProperties shapeProperties4 = new Xdr.ShapeProperties();

            A.Transform2D transform2D4 = new A.Transform2D();
            A.Offset offset6 = new A.Offset() { X = 11772900L, Y = 57150L };
            A.Extents extents6 = new A.Extents() { Cx = 10058400L, Cy = 5657850L };

            transform2D4.Append(offset6);
            transform2D4.Append(extents6);

            A.PresetGeometry presetGeometry4 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList4 = new A.AdjustValueList();

            presetGeometry4.Append(adjustValueList4);

            shapeProperties4.Append(transform2D4);
            shapeProperties4.Append(presetGeometry4);

            picture2.Append(nonVisualPictureProperties2);
            picture2.Append(blipFill2);
            picture2.Append(shapeProperties4);
            Xdr.ClientData clientData6 = new Xdr.ClientData();

            twoCellAnchor6.Append(fromMarker6);
            twoCellAnchor6.Append(toMarker6);
            twoCellAnchor6.Append(picture2);
            twoCellAnchor6.Append(clientData6);

            worksheetDrawing1.Append(twoCellAnchor1);
            worksheetDrawing1.Append(twoCellAnchor2);
            worksheetDrawing1.Append(twoCellAnchor3);
            worksheetDrawing1.Append(twoCellAnchor4);
            worksheetDrawing1.Append(twoCellAnchor5);
            worksheetDrawing1.Append(twoCellAnchor6);

            drawingsPart1.WorksheetDrawing = worksheetDrawing1;
        }

        // Generates content of imagePart for small image on cover sheet
        private void generateImageCoverSmall(ImagePart coverSmallPart)
        {
            System.IO.Stream data = GetBinaryDataStream(coverSmallPartData);
            coverSmallPart.FeedData(data);
            data.Close();
        }

        // Generates content of imagePart1.
        private void GenerateImagePart1Content(ImagePart imagePart1)
        {
            System.IO.Stream data = GetBinaryDataStream(imagePart1Data);
            imagePart1.FeedData(data);
            data.Close();
        }

        // Generates content of chartPart1.
        private void GenerateChartPart1Content(ChartPart chartPart1)
        {
            C.ChartSpace chartSpace1 = new C.ChartSpace();
            chartSpace1.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
            chartSpace1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            chartSpace1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            chartSpace1.AddNamespaceDeclaration("c16r2", "http://schemas.microsoft.com/office/drawing/2015/06/chart");
            C.Date1904 date19041 = new C.Date1904() { Val = false };
            C.EditingLanguage editingLanguage1 = new C.EditingLanguage() { Val = "en-US" };
            C.RoundedCorners roundedCorners1 = new C.RoundedCorners() { Val = false };

            AlternateContent alternateContent2 = new AlternateContent();
            alternateContent2.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");

            AlternateContentChoice alternateContentChoice2 = new AlternateContentChoice() { Requires = "c14" };
            alternateContentChoice2.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            C14.Style style1 = new C14.Style() { Val = 102 };

            alternateContentChoice2.Append(style1);

            AlternateContentFallback alternateContentFallback1 = new AlternateContentFallback();
            C.Style style2 = new C.Style() { Val = 2 };

            alternateContentFallback1.Append(style2);

            alternateContent2.Append(alternateContentChoice2);
            alternateContent2.Append(alternateContentFallback1);

            C.PivotSource pivotSource1 = new C.PivotSource();
            C.PivotTableName pivotTableName1 = new C.PivotTableName();
            pivotTableName1.Text = "[focustest.xlsx]Cover!PivotTable23";
            C.FormatId formatId1 = new C.FormatId() { Val = (UInt32Value)9U };

            pivotSource1.Append(pivotTableName1);
            pivotSource1.Append(formatId1);

            C.Chart chart1 = new C.Chart();

            C.Title title1 = new C.Title();

            C.ChartText chartText1 = new C.ChartText();

            C.RichText richText1 = new C.RichText();
            A.BodyProperties bodyProperties3 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ListStyle listStyle3 = new A.ListStyle();

            A.Paragraph paragraph3 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1400, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Spacing = 0, Baseline = 0 };

            A.SolidFill solidFill5 = new A.SolidFill();

            A.SchemeColor schemeColor7 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation1 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset1 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor7.Append(luminanceModulation1);
            schemeColor7.Append(luminanceOffset1);

            solidFill5.Append(schemeColor7);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont1 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties1.Append(solidFill5);
            defaultRunProperties1.Append(latinFont1);
            defaultRunProperties1.Append(eastAsianFont1);
            defaultRunProperties1.Append(complexScriptFont1);

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run4 = new A.Run();
            A.RunProperties runProperties4 = new A.RunProperties() { Language = "en-US" };
            A.Text text58 = new A.Text();
            text58.Text = "Overall";

            run4.Append(runProperties4);
            run4.Append(text58);

            A.Run run5 = new A.Run();
            A.RunProperties runProperties5 = new A.RunProperties() { Language = "en-US", Baseline = 0 };
            A.Text text59 = new A.Text();
            text59.Text = " Progress";

            run5.Append(runProperties5);
            run5.Append(text59);
            A.EndParagraphRunProperties endParagraphRunProperties2 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph3.Append(paragraphProperties1);
            paragraph3.Append(run4);
            paragraph3.Append(run5);
            paragraph3.Append(endParagraphRunProperties2);

            richText1.Append(bodyProperties3);
            richText1.Append(listStyle3);
            richText1.Append(paragraph3);

            chartText1.Append(richText1);
            C.Overlay overlay1 = new C.Overlay() { Val = false };

            C.ChartShapeProperties chartShapeProperties1 = new C.ChartShapeProperties();
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline3 = new A.Outline();
            A.NoFill noFill2 = new A.NoFill();

            outline3.Append(noFill2);
            A.EffectList effectList1 = new A.EffectList();

            chartShapeProperties1.Append(noFill1);
            chartShapeProperties1.Append(outline3);
            chartShapeProperties1.Append(effectList1);

            C.TextProperties textProperties1 = new C.TextProperties();
            A.BodyProperties bodyProperties4 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ListStyle listStyle4 = new A.ListStyle();

            A.Paragraph paragraph4 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties2 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties2 = new A.DefaultRunProperties() { FontSize = 1400, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Spacing = 0, Baseline = 0 };

            A.SolidFill solidFill6 = new A.SolidFill();

            A.SchemeColor schemeColor8 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation2 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset2 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor8.Append(luminanceModulation2);
            schemeColor8.Append(luminanceOffset2);

            solidFill6.Append(schemeColor8);
            A.LatinFont latinFont2 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont2 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont2 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties2.Append(solidFill6);
            defaultRunProperties2.Append(latinFont2);
            defaultRunProperties2.Append(eastAsianFont2);
            defaultRunProperties2.Append(complexScriptFont2);

            paragraphProperties2.Append(defaultRunProperties2);
            A.EndParagraphRunProperties endParagraphRunProperties3 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph4.Append(paragraphProperties2);
            paragraph4.Append(endParagraphRunProperties3);

            textProperties1.Append(bodyProperties4);
            textProperties1.Append(listStyle4);
            textProperties1.Append(paragraph4);

            title1.Append(chartText1);
            title1.Append(overlay1);
            title1.Append(chartShapeProperties1);
            title1.Append(textProperties1);
            C.AutoTitleDeleted autoTitleDeleted1 = new C.AutoTitleDeleted() { Val = false };

            C.PivotFormats pivotFormats1 = new C.PivotFormats();

            C.PivotFormat pivotFormat1 = new C.PivotFormat();
            C.Index index1 = new C.Index() { Val = (UInt32Value)0U };

            C.ShapeProperties shapeProperties5 = new C.ShapeProperties();

            A.SolidFill solidFill7 = new A.SolidFill();
            A.SchemeColor schemeColor9 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent1 };

            solidFill7.Append(schemeColor9);

            A.Outline outline4 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill8 = new A.SolidFill();
            A.SchemeColor schemeColor10 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill8.Append(schemeColor10);

            outline4.Append(solidFill8);
            A.EffectList effectList2 = new A.EffectList();

            shapeProperties5.Append(solidFill7);
            shapeProperties5.Append(outline4);
            shapeProperties5.Append(effectList2);

            C.Marker marker1 = new C.Marker();
            C.Symbol symbol1 = new C.Symbol() { Val = C.MarkerStyleValues.None };

            marker1.Append(symbol1);

            C.DataLabel dataLabel1 = new C.DataLabel();
            C.Index index2 = new C.Index() { Val = (UInt32Value)0U };

            C.ChartShapeProperties chartShapeProperties2 = new C.ChartShapeProperties();
            A.NoFill noFill3 = new A.NoFill();

            A.Outline outline5 = new A.Outline();
            A.NoFill noFill4 = new A.NoFill();

            outline5.Append(noFill4);
            A.EffectList effectList3 = new A.EffectList();

            chartShapeProperties2.Append(noFill3);
            chartShapeProperties2.Append(outline5);
            chartShapeProperties2.Append(effectList3);

            C.TextProperties textProperties2 = new C.TextProperties();

            A.BodyProperties bodyProperties5 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties5.Append(shapeAutoFit1);
            A.ListStyle listStyle5 = new A.ListStyle();

            A.Paragraph paragraph5 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties3 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties3 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill9 = new A.SolidFill();

            A.SchemeColor schemeColor11 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation3 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset3 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor11.Append(luminanceModulation3);
            schemeColor11.Append(luminanceOffset3);

            solidFill9.Append(schemeColor11);
            A.LatinFont latinFont3 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont3 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont3 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties3.Append(solidFill9);
            defaultRunProperties3.Append(latinFont3);
            defaultRunProperties3.Append(eastAsianFont3);
            defaultRunProperties3.Append(complexScriptFont3);

            paragraphProperties3.Append(defaultRunProperties3);
            A.EndParagraphRunProperties endParagraphRunProperties4 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph5.Append(paragraphProperties3);
            paragraph5.Append(endParagraphRunProperties4);

            textProperties2.Append(bodyProperties5);
            textProperties2.Append(listStyle5);
            textProperties2.Append(paragraph5);
            C.ShowLegendKey showLegendKey1 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue1 = new C.ShowValue() { Val = false };
            C.ShowCategoryName showCategoryName1 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName1 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent1 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize1 = new C.ShowBubbleSize() { Val = false };

            C.DLblExtensionList dLblExtensionList1 = new C.DLblExtensionList();

            C.DLblExtension dLblExtension1 = new C.DLblExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblExtension1.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");

            dLblExtensionList1.Append(dLblExtension1);

            dataLabel1.Append(index2);
            dataLabel1.Append(chartShapeProperties2);
            dataLabel1.Append(textProperties2);
            dataLabel1.Append(showLegendKey1);
            dataLabel1.Append(showValue1);
            dataLabel1.Append(showCategoryName1);
            dataLabel1.Append(showSeriesName1);
            dataLabel1.Append(showPercent1);
            dataLabel1.Append(showBubbleSize1);
            dataLabel1.Append(dLblExtensionList1);

            pivotFormat1.Append(index1);
            pivotFormat1.Append(shapeProperties5);
            pivotFormat1.Append(marker1);
            pivotFormat1.Append(dataLabel1);

            C.PivotFormat pivotFormat2 = new C.PivotFormat();
            C.Index index3 = new C.Index() { Val = (UInt32Value)1U };

            C.ShapeProperties shapeProperties6 = new C.ShapeProperties();

            A.SolidFill solidFill10 = new A.SolidFill();

            A.SchemeColor schemeColor12 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent2 };
            A.LuminanceModulation luminanceModulation4 = new A.LuminanceModulation() { Val = 60000 };
            A.LuminanceOffset luminanceOffset4 = new A.LuminanceOffset() { Val = 40000 };

            schemeColor12.Append(luminanceModulation4);
            schemeColor12.Append(luminanceOffset4);

            solidFill10.Append(schemeColor12);

            A.Outline outline6 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill11 = new A.SolidFill();
            A.SchemeColor schemeColor13 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill11.Append(schemeColor13);

            outline6.Append(solidFill11);
            A.EffectList effectList4 = new A.EffectList();

            shapeProperties6.Append(solidFill10);
            shapeProperties6.Append(outline6);
            shapeProperties6.Append(effectList4);

            pivotFormat2.Append(index3);
            pivotFormat2.Append(shapeProperties6);

            C.PivotFormat pivotFormat3 = new C.PivotFormat();
            C.Index index4 = new C.Index() { Val = (UInt32Value)2U };

            C.ShapeProperties shapeProperties7 = new C.ShapeProperties();

            A.SolidFill solidFill12 = new A.SolidFill();
            A.SchemeColor schemeColor14 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent6 };

            solidFill12.Append(schemeColor14);

            A.Outline outline7 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill13 = new A.SolidFill();
            A.SchemeColor schemeColor15 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill13.Append(schemeColor15);

            outline7.Append(solidFill13);
            A.EffectList effectList5 = new A.EffectList();

            shapeProperties7.Append(solidFill12);
            shapeProperties7.Append(outline7);
            shapeProperties7.Append(effectList5);

            pivotFormat3.Append(index4);
            pivotFormat3.Append(shapeProperties7);

            C.PivotFormat pivotFormat4 = new C.PivotFormat();
            C.Index index5 = new C.Index() { Val = (UInt32Value)3U };

            C.ShapeProperties shapeProperties8 = new C.ShapeProperties();

            A.SolidFill solidFill14 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "FF4B4B" };

            solidFill14.Append(rgbColorModelHex1);

            A.Outline outline8 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill15 = new A.SolidFill();
            A.SchemeColor schemeColor16 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill15.Append(schemeColor16);

            outline8.Append(solidFill15);
            A.EffectList effectList6 = new A.EffectList();

            shapeProperties8.Append(solidFill14);
            shapeProperties8.Append(outline8);
            shapeProperties8.Append(effectList6);

            pivotFormat4.Append(index5);
            pivotFormat4.Append(shapeProperties8);

            pivotFormats1.Append(pivotFormat1);
            pivotFormats1.Append(pivotFormat2);
            pivotFormats1.Append(pivotFormat3);
            pivotFormats1.Append(pivotFormat4);

            C.PlotArea plotArea1 = new C.PlotArea();
            C.Layout layout1 = new C.Layout();

            C.DoughnutChart doughnutChart1 = new C.DoughnutChart();
            C.VaryColors varyColors1 = new C.VaryColors() { Val = true };

            C.PieChartSeries pieChartSeries1 = new C.PieChartSeries();
            C.Index index6 = new C.Index() { Val = (UInt32Value)0U };
            C.Order order1 = new C.Order() { Val = (UInt32Value)0U };

            C.SeriesText seriesText1 = new C.SeriesText();

            C.StringReference stringReference1 = new C.StringReference();
            C.Formula formula1 = new C.Formula();
            formula1.Text = "Cover!$I$34";

            C.StringCache stringCache1 = new C.StringCache();
            C.PointCount pointCount1 = new C.PointCount() { Val = (UInt32Value)1U };

            C.StringPoint stringPoint1 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue1 = new C.NumericValue();
            numericValue1.Text = "Total";

            stringPoint1.Append(numericValue1);

            stringCache1.Append(pointCount1);
            stringCache1.Append(stringPoint1);

            stringReference1.Append(formula1);
            stringReference1.Append(stringCache1);

            seriesText1.Append(stringReference1);

            C.DataPoint dataPoint1 = new C.DataPoint();
            C.Index index7 = new C.Index() { Val = (UInt32Value)0U };
            C.Bubble3D bubble3D1 = new C.Bubble3D() { Val = false };

            C.ChartShapeProperties chartShapeProperties3 = new C.ChartShapeProperties();

            A.SolidFill solidFill16 = new A.SolidFill();
            A.SchemeColor schemeColor17 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent6 };

            solidFill16.Append(schemeColor17);

            A.Outline outline9 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill17 = new A.SolidFill();
            A.SchemeColor schemeColor18 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill17.Append(schemeColor18);

            outline9.Append(solidFill17);
            A.EffectList effectList7 = new A.EffectList();

            chartShapeProperties3.Append(solidFill16);
            chartShapeProperties3.Append(outline9);
            chartShapeProperties3.Append(effectList7);

            C.ExtensionList extensionList1 = new C.ExtensionList();
            extensionList1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            extensionList1.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            extensionList1.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            extensionList1.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            extensionList1.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.Extension extension2 = new C.Extension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            extension2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement12 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000002-FAA8-4A8E-9C8C-2DF88A897091}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            extension2.Append(openXmlUnknownElement12);

            extensionList1.Append(extension2);

            dataPoint1.Append(index7);
            dataPoint1.Append(bubble3D1);
            dataPoint1.Append(chartShapeProperties3);
            dataPoint1.Append(extensionList1);

            C.DataPoint dataPoint2 = new C.DataPoint();
            C.Index index8 = new C.Index() { Val = (UInt32Value)1U };
            C.Bubble3D bubble3D2 = new C.Bubble3D() { Val = false };

            C.ChartShapeProperties chartShapeProperties4 = new C.ChartShapeProperties();

            A.SolidFill solidFill18 = new A.SolidFill();

            A.SchemeColor schemeColor19 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent2 };
            A.LuminanceModulation luminanceModulation5 = new A.LuminanceModulation() { Val = 60000 };
            A.LuminanceOffset luminanceOffset5 = new A.LuminanceOffset() { Val = 40000 };

            schemeColor19.Append(luminanceModulation5);
            schemeColor19.Append(luminanceOffset5);

            solidFill18.Append(schemeColor19);

            A.Outline outline10 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill19 = new A.SolidFill();
            A.SchemeColor schemeColor20 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill19.Append(schemeColor20);

            outline10.Append(solidFill19);
            A.EffectList effectList8 = new A.EffectList();

            chartShapeProperties4.Append(solidFill18);
            chartShapeProperties4.Append(outline10);
            chartShapeProperties4.Append(effectList8);

            C.ExtensionList extensionList2 = new C.ExtensionList();
            extensionList2.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            extensionList2.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            extensionList2.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            extensionList2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            extensionList2.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.Extension extension3 = new C.Extension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            extension3.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement13 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000001-FAA8-4A8E-9C8C-2DF88A897091}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            extension3.Append(openXmlUnknownElement13);

            extensionList2.Append(extension3);

            dataPoint2.Append(index8);
            dataPoint2.Append(bubble3D2);
            dataPoint2.Append(chartShapeProperties4);
            dataPoint2.Append(extensionList2);

            C.DataPoint dataPoint3 = new C.DataPoint();
            C.Index index9 = new C.Index() { Val = (UInt32Value)2U };
            C.Bubble3D bubble3D3 = new C.Bubble3D() { Val = false };

            C.ChartShapeProperties chartShapeProperties5 = new C.ChartShapeProperties();

            A.SolidFill solidFill20 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex2 = new A.RgbColorModelHex() { Val = "FF4B4B" };

            solidFill20.Append(rgbColorModelHex2);

            A.Outline outline11 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill21 = new A.SolidFill();
            A.SchemeColor schemeColor21 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill21.Append(schemeColor21);

            outline11.Append(solidFill21);
            A.EffectList effectList9 = new A.EffectList();

            chartShapeProperties5.Append(solidFill20);
            chartShapeProperties5.Append(outline11);
            chartShapeProperties5.Append(effectList9);

            C.ExtensionList extensionList3 = new C.ExtensionList();
            extensionList3.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            extensionList3.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            extensionList3.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            extensionList3.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            extensionList3.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.Extension extension4 = new C.Extension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            extension4.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement14 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000003-FAA8-4A8E-9C8C-2DF88A897091}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            extension4.Append(openXmlUnknownElement14);

            extensionList3.Append(extension4);

            dataPoint3.Append(index9);
            dataPoint3.Append(bubble3D3);
            dataPoint3.Append(chartShapeProperties5);
            dataPoint3.Append(extensionList3);

            C.CategoryAxisData categoryAxisData1 = new C.CategoryAxisData();

            C.StringReference stringReference2 = new C.StringReference();
            C.Formula formula2 = new C.Formula();
            formula2.Text = "Cover!$H$35:$H$38";

            C.StringCache stringCache2 = new C.StringCache();
            C.PointCount pointCount2 = new C.PointCount() { Val = (UInt32Value)3U };

            C.StringPoint stringPoint2 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue2 = new C.NumericValue();
            numericValue2.Text = "Completed";

            stringPoint2.Append(numericValue2);

            C.StringPoint stringPoint3 = new C.StringPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue3 = new C.NumericValue();
            numericValue3.Text = "In Progress";

            stringPoint3.Append(numericValue3);

            C.StringPoint stringPoint4 = new C.StringPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue4 = new C.NumericValue();
            numericValue4.Text = "Not started";

            stringPoint4.Append(numericValue4);

            stringCache2.Append(pointCount2);
            stringCache2.Append(stringPoint2);
            stringCache2.Append(stringPoint3);
            stringCache2.Append(stringPoint4);

            stringReference2.Append(formula2);
            stringReference2.Append(stringCache2);

            categoryAxisData1.Append(stringReference2);

            C.Values values1 = new C.Values();

            C.NumberReference numberReference1 = new C.NumberReference();
            C.Formula formula3 = new C.Formula();
            formula3.Text = "Cover!$I$35:$I$38";

            C.NumberingCache numberingCache1 = new C.NumberingCache();
            C.FormatCode formatCode1 = new C.FormatCode();
            formatCode1.Text = "General";
            C.PointCount pointCount3 = new C.PointCount() { Val = (UInt32Value)3U };

            C.NumericPoint numericPoint1 = new C.NumericPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue5 = new C.NumericValue();
            numericValue5.Text = "1";

            numericPoint1.Append(numericValue5);

            C.NumericPoint numericPoint2 = new C.NumericPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue6 = new C.NumericValue();
            numericValue6.Text = "3";

            numericPoint2.Append(numericValue6);

            C.NumericPoint numericPoint3 = new C.NumericPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue7 = new C.NumericValue();
            numericValue7.Text = "3";

            numericPoint3.Append(numericValue7);

            numberingCache1.Append(formatCode1);
            numberingCache1.Append(pointCount3);
            numberingCache1.Append(numericPoint1);
            numberingCache1.Append(numericPoint2);
            numberingCache1.Append(numericPoint3);

            numberReference1.Append(formula3);
            numberReference1.Append(numberingCache1);

            values1.Append(numberReference1);

            C.PieSerExtensionList pieSerExtensionList1 = new C.PieSerExtensionList();
            pieSerExtensionList1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            pieSerExtensionList1.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            pieSerExtensionList1.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            pieSerExtensionList1.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            pieSerExtensionList1.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.PieSerExtension pieSerExtension1 = new C.PieSerExtension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            pieSerExtension1.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement15 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000000-FAA8-4A8E-9C8C-2DF88A897091}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            pieSerExtension1.Append(openXmlUnknownElement15);

            pieSerExtensionList1.Append(pieSerExtension1);

            pieChartSeries1.Append(index6);
            pieChartSeries1.Append(order1);
            pieChartSeries1.Append(seriesText1);
            pieChartSeries1.Append(dataPoint1);
            pieChartSeries1.Append(dataPoint2);
            pieChartSeries1.Append(dataPoint3);
            pieChartSeries1.Append(categoryAxisData1);
            pieChartSeries1.Append(values1);
            pieChartSeries1.Append(pieSerExtensionList1);

            C.DataLabels dataLabels1 = new C.DataLabels();
            C.ShowLegendKey showLegendKey2 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue2 = new C.ShowValue() { Val = false };
            C.ShowCategoryName showCategoryName2 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName2 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent2 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize2 = new C.ShowBubbleSize() { Val = false };
            C.ShowLeaderLines showLeaderLines1 = new C.ShowLeaderLines() { Val = true };

            dataLabels1.Append(showLegendKey2);
            dataLabels1.Append(showValue2);
            dataLabels1.Append(showCategoryName2);
            dataLabels1.Append(showSeriesName2);
            dataLabels1.Append(showPercent2);
            dataLabels1.Append(showBubbleSize2);
            dataLabels1.Append(showLeaderLines1);
            C.FirstSliceAngle firstSliceAngle1 = new C.FirstSliceAngle() { Val = (UInt16Value)0U };
            C.HoleSize holeSize1 = new C.HoleSize() { Val = 75 };

            doughnutChart1.Append(varyColors1);
            doughnutChart1.Append(pieChartSeries1);
            doughnutChart1.Append(dataLabels1);
            doughnutChart1.Append(firstSliceAngle1);
            doughnutChart1.Append(holeSize1);

            C.ShapeProperties shapeProperties9 = new C.ShapeProperties();
            A.NoFill noFill5 = new A.NoFill();

            A.Outline outline12 = new A.Outline();
            A.NoFill noFill6 = new A.NoFill();

            outline12.Append(noFill6);
            A.EffectList effectList10 = new A.EffectList();

            shapeProperties9.Append(noFill5);
            shapeProperties9.Append(outline12);
            shapeProperties9.Append(effectList10);

            plotArea1.Append(layout1);
            plotArea1.Append(doughnutChart1);
            plotArea1.Append(shapeProperties9);

            C.Legend legend1 = new C.Legend();
            C.LegendPosition legendPosition1 = new C.LegendPosition() { Val = C.LegendPositionValues.Right };
            C.Overlay overlay2 = new C.Overlay() { Val = false };

            C.ChartShapeProperties chartShapeProperties6 = new C.ChartShapeProperties();
            A.NoFill noFill7 = new A.NoFill();

            A.Outline outline13 = new A.Outline();
            A.NoFill noFill8 = new A.NoFill();

            outline13.Append(noFill8);
            A.EffectList effectList11 = new A.EffectList();

            chartShapeProperties6.Append(noFill7);
            chartShapeProperties6.Append(outline13);
            chartShapeProperties6.Append(effectList11);

            C.TextProperties textProperties3 = new C.TextProperties();
            A.BodyProperties bodyProperties6 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ListStyle listStyle6 = new A.ListStyle();

            A.Paragraph paragraph6 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties4 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties4 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill22 = new A.SolidFill();

            A.SchemeColor schemeColor22 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation6 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset6 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor22.Append(luminanceModulation6);
            schemeColor22.Append(luminanceOffset6);

            solidFill22.Append(schemeColor22);
            A.LatinFont latinFont4 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont4 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont4 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties4.Append(solidFill22);
            defaultRunProperties4.Append(latinFont4);
            defaultRunProperties4.Append(eastAsianFont4);
            defaultRunProperties4.Append(complexScriptFont4);

            paragraphProperties4.Append(defaultRunProperties4);
            A.EndParagraphRunProperties endParagraphRunProperties5 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph6.Append(paragraphProperties4);
            paragraph6.Append(endParagraphRunProperties5);

            textProperties3.Append(bodyProperties6);
            textProperties3.Append(listStyle6);
            textProperties3.Append(paragraph6);

            legend1.Append(legendPosition1);
            legend1.Append(overlay2);
            legend1.Append(chartShapeProperties6);
            legend1.Append(textProperties3);
            C.PlotVisibleOnly plotVisibleOnly1 = new C.PlotVisibleOnly() { Val = true };
            C.DisplayBlanksAs displayBlanksAs1 = new C.DisplayBlanksAs() { Val = C.DisplayBlanksAsValues.Gap };

            C.ExtensionList extensionList4 = new C.ExtensionList();
            extensionList4.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            extensionList4.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            extensionList4.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            extensionList4.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            extensionList4.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.Extension extension5 = new C.Extension() { Uri = "{56B9EC1D-385E-4148-901F-78D8002777C0}" };
            extension5.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            OpenXmlUnknownElement openXmlUnknownElement16 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16r3:dataDisplayOptions16 xmlns:c16r3=\"http://schemas.microsoft.com/office/drawing/2017/03/chart\"><c16r3:dispNaAsBlank val=\"1\" /></c16r3:dataDisplayOptions16>");

            extension5.Append(openXmlUnknownElement16);

            extensionList4.Append(extension5);
            C.ShowDataLabelsOverMaximum showDataLabelsOverMaximum1 = new C.ShowDataLabelsOverMaximum() { Val = false };

            chart1.Append(title1);
            chart1.Append(autoTitleDeleted1);
            chart1.Append(pivotFormats1);
            chart1.Append(plotArea1);
            chart1.Append(legend1);
            chart1.Append(plotVisibleOnly1);
            chart1.Append(displayBlanksAs1);
            chart1.Append(extensionList4);
            chart1.Append(showDataLabelsOverMaximum1);

            C.ShapeProperties shapeProperties10 = new C.ShapeProperties();

            A.SolidFill solidFill23 = new A.SolidFill();
            A.SchemeColor schemeColor23 = new A.SchemeColor() { Val = A.SchemeColorValues.Background1 };

            solidFill23.Append(schemeColor23);

            A.Outline outline14 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill24 = new A.SolidFill();

            A.SchemeColor schemeColor24 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation7 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset7 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor24.Append(luminanceModulation7);
            schemeColor24.Append(luminanceOffset7);

            solidFill24.Append(schemeColor24);
            A.Round round1 = new A.Round();

            outline14.Append(solidFill24);
            outline14.Append(round1);
            A.EffectList effectList12 = new A.EffectList();

            shapeProperties10.Append(solidFill23);
            shapeProperties10.Append(outline14);
            shapeProperties10.Append(effectList12);

            C.TextProperties textProperties4 = new C.TextProperties();
            A.BodyProperties bodyProperties7 = new A.BodyProperties();
            A.ListStyle listStyle7 = new A.ListStyle();

            A.Paragraph paragraph7 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties5 = new A.ParagraphProperties();
            A.DefaultRunProperties defaultRunProperties5 = new A.DefaultRunProperties();

            paragraphProperties5.Append(defaultRunProperties5);
            A.EndParagraphRunProperties endParagraphRunProperties6 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph7.Append(paragraphProperties5);
            paragraph7.Append(endParagraphRunProperties6);

            textProperties4.Append(bodyProperties7);
            textProperties4.Append(listStyle7);
            textProperties4.Append(paragraph7);

            C.PrintSettings printSettings1 = new C.PrintSettings();
            C.HeaderFooter headerFooter1 = new C.HeaderFooter();
            C.PageMargins pageMargins2 = new C.PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            C.PageSetup pageSetup2 = new C.PageSetup();

            printSettings1.Append(headerFooter1);
            printSettings1.Append(pageMargins2);
            printSettings1.Append(pageSetup2);

            C.ChartSpaceExtensionList chartSpaceExtensionList1 = new C.ChartSpaceExtensionList();
            chartSpaceExtensionList1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            chartSpaceExtensionList1.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            chartSpaceExtensionList1.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            chartSpaceExtensionList1.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            chartSpaceExtensionList1.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.ChartSpaceExtension chartSpaceExtension1 = new C.ChartSpaceExtension() { Uri = "{781A3756-C4B2-4CAC-9D66-4F8BD8637D16}" };
            chartSpaceExtension1.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");

            C14.PivotOptions pivotOptions1 = new C14.PivotOptions();
            C14.DropZoneFilter dropZoneFilter1 = new C14.DropZoneFilter() { Val = true };
            C14.DropZoneCategories dropZoneCategories1 = new C14.DropZoneCategories() { Val = true };
            C14.DropZoneData dropZoneData1 = new C14.DropZoneData() { Val = true };
            C14.DropZoneSeries dropZoneSeries1 = new C14.DropZoneSeries() { Val = true };
            C14.DropZonesVisible dropZonesVisible1 = new C14.DropZonesVisible() { Val = true };

            pivotOptions1.Append(dropZoneFilter1);
            pivotOptions1.Append(dropZoneCategories1);
            pivotOptions1.Append(dropZoneData1);
            pivotOptions1.Append(dropZoneSeries1);
            pivotOptions1.Append(dropZonesVisible1);

            chartSpaceExtension1.Append(pivotOptions1);

            C.ChartSpaceExtension chartSpaceExtension2 = new C.ChartSpaceExtension() { Uri = "{E28EC0CA-F0BB-4C9C-879D-F8772B89E7AC}" };
            chartSpaceExtension2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement17 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:pivotOptions16 xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\"><c16:showExpandCollapseFieldButtons val=\"1\" /></c16:pivotOptions16>");

            chartSpaceExtension2.Append(openXmlUnknownElement17);

            chartSpaceExtensionList1.Append(chartSpaceExtension1);
            chartSpaceExtensionList1.Append(chartSpaceExtension2);

            chartSpace1.Append(date19041);
            chartSpace1.Append(editingLanguage1);
            chartSpace1.Append(roundedCorners1);
            chartSpace1.Append(alternateContent2);
            chartSpace1.Append(pivotSource1);
            chartSpace1.Append(chart1);
            chartSpace1.Append(shapeProperties10);
            chartSpace1.Append(textProperties4);
            chartSpace1.Append(printSettings1);
            chartSpace1.Append(chartSpaceExtensionList1);

            chartPart1.ChartSpace = chartSpace1;
        }

        // Generates content of chartColorStylePart1.
        private void GenerateChartColorStylePart1Content(ChartColorStylePart chartColorStylePart1)
        {
            Cs.ColorStyle colorStyle1 = new Cs.ColorStyle() { Method = "cycle", Id = (UInt32Value)10U };
            colorStyle1.AddNamespaceDeclaration("cs", "http://schemas.microsoft.com/office/drawing/2012/chartStyle");
            colorStyle1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            A.SchemeColor schemeColor25 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent1 };
            A.SchemeColor schemeColor26 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent2 };
            A.SchemeColor schemeColor27 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent3 };
            A.SchemeColor schemeColor28 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent4 };
            A.SchemeColor schemeColor29 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent5 };
            A.SchemeColor schemeColor30 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent6 };
            Cs.ColorStyleVariation colorStyleVariation1 = new Cs.ColorStyleVariation();

            Cs.ColorStyleVariation colorStyleVariation2 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation8 = new A.LuminanceModulation() { Val = 60000 };

            colorStyleVariation2.Append(luminanceModulation8);

            Cs.ColorStyleVariation colorStyleVariation3 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation9 = new A.LuminanceModulation() { Val = 80000 };
            A.LuminanceOffset luminanceOffset8 = new A.LuminanceOffset() { Val = 20000 };

            colorStyleVariation3.Append(luminanceModulation9);
            colorStyleVariation3.Append(luminanceOffset8);

            Cs.ColorStyleVariation colorStyleVariation4 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation10 = new A.LuminanceModulation() { Val = 80000 };

            colorStyleVariation4.Append(luminanceModulation10);

            Cs.ColorStyleVariation colorStyleVariation5 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation11 = new A.LuminanceModulation() { Val = 60000 };
            A.LuminanceOffset luminanceOffset9 = new A.LuminanceOffset() { Val = 40000 };

            colorStyleVariation5.Append(luminanceModulation11);
            colorStyleVariation5.Append(luminanceOffset9);

            Cs.ColorStyleVariation colorStyleVariation6 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation12 = new A.LuminanceModulation() { Val = 50000 };

            colorStyleVariation6.Append(luminanceModulation12);

            Cs.ColorStyleVariation colorStyleVariation7 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation13 = new A.LuminanceModulation() { Val = 70000 };
            A.LuminanceOffset luminanceOffset10 = new A.LuminanceOffset() { Val = 30000 };

            colorStyleVariation7.Append(luminanceModulation13);
            colorStyleVariation7.Append(luminanceOffset10);

            Cs.ColorStyleVariation colorStyleVariation8 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation14 = new A.LuminanceModulation() { Val = 70000 };

            colorStyleVariation8.Append(luminanceModulation14);

            Cs.ColorStyleVariation colorStyleVariation9 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation15 = new A.LuminanceModulation() { Val = 50000 };
            A.LuminanceOffset luminanceOffset11 = new A.LuminanceOffset() { Val = 50000 };

            colorStyleVariation9.Append(luminanceModulation15);
            colorStyleVariation9.Append(luminanceOffset11);

            colorStyle1.Append(schemeColor25);
            colorStyle1.Append(schemeColor26);
            colorStyle1.Append(schemeColor27);
            colorStyle1.Append(schemeColor28);
            colorStyle1.Append(schemeColor29);
            colorStyle1.Append(schemeColor30);
            colorStyle1.Append(colorStyleVariation1);
            colorStyle1.Append(colorStyleVariation2);
            colorStyle1.Append(colorStyleVariation3);
            colorStyle1.Append(colorStyleVariation4);
            colorStyle1.Append(colorStyleVariation5);
            colorStyle1.Append(colorStyleVariation6);
            colorStyle1.Append(colorStyleVariation7);
            colorStyle1.Append(colorStyleVariation8);
            colorStyle1.Append(colorStyleVariation9);

            chartColorStylePart1.ColorStyle = colorStyle1;
        }

        // Generates content of chartStylePart1.
        private void GenerateChartStylePart1Content(ChartStylePart chartStylePart1)
        {
            Cs.ChartStyle chartStyle1 = new Cs.ChartStyle() { Id = (UInt32Value)251U };
            chartStyle1.AddNamespaceDeclaration("cs", "http://schemas.microsoft.com/office/drawing/2012/chartStyle");
            chartStyle1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            Cs.AxisTitle axisTitle1 = new Cs.AxisTitle();
            Cs.LineReference lineReference3 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference3 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference3 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference3 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor31 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation16 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset12 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor31.Append(luminanceModulation16);
            schemeColor31.Append(luminanceOffset12);

            fontReference3.Append(schemeColor31);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType1 = new Cs.TextCharacterPropertiesType() { FontSize = 1000, Kerning = 1200 };

            axisTitle1.Append(lineReference3);
            axisTitle1.Append(fillReference3);
            axisTitle1.Append(effectReference3);
            axisTitle1.Append(fontReference3);
            axisTitle1.Append(textCharacterPropertiesType1);

            Cs.CategoryAxis categoryAxis1 = new Cs.CategoryAxis();
            Cs.LineReference lineReference4 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference4 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference4 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference4 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor32 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation17 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset13 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor32.Append(luminanceModulation17);
            schemeColor32.Append(luminanceOffset13);

            fontReference4.Append(schemeColor32);

            Cs.ShapeProperties shapeProperties11 = new Cs.ShapeProperties();

            A.Outline outline15 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill25 = new A.SolidFill();

            A.SchemeColor schemeColor33 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation18 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset14 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor33.Append(luminanceModulation18);
            schemeColor33.Append(luminanceOffset14);

            solidFill25.Append(schemeColor33);
            A.Round round2 = new A.Round();

            outline15.Append(solidFill25);
            outline15.Append(round2);

            shapeProperties11.Append(outline15);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType2 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            categoryAxis1.Append(lineReference4);
            categoryAxis1.Append(fillReference4);
            categoryAxis1.Append(effectReference4);
            categoryAxis1.Append(fontReference4);
            categoryAxis1.Append(shapeProperties11);
            categoryAxis1.Append(textCharacterPropertiesType2);

            Cs.ChartArea chartArea1 = new Cs.ChartArea() { Modifiers = new ListValue<StringValue>() { InnerText = "allowNoFillOverride allowNoLineOverride" } };
            Cs.LineReference lineReference5 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference5 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference5 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference5 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor34 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference5.Append(schemeColor34);

            Cs.ShapeProperties shapeProperties12 = new Cs.ShapeProperties();

            A.SolidFill solidFill26 = new A.SolidFill();
            A.SchemeColor schemeColor35 = new A.SchemeColor() { Val = A.SchemeColorValues.Background1 };

            solidFill26.Append(schemeColor35);

            A.Outline outline16 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill27 = new A.SolidFill();

            A.SchemeColor schemeColor36 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation19 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset15 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor36.Append(luminanceModulation19);
            schemeColor36.Append(luminanceOffset15);

            solidFill27.Append(schemeColor36);
            A.Round round3 = new A.Round();

            outline16.Append(solidFill27);
            outline16.Append(round3);

            shapeProperties12.Append(solidFill26);
            shapeProperties12.Append(outline16);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType3 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            chartArea1.Append(lineReference5);
            chartArea1.Append(fillReference5);
            chartArea1.Append(effectReference5);
            chartArea1.Append(fontReference5);
            chartArea1.Append(shapeProperties12);
            chartArea1.Append(textCharacterPropertiesType3);

            Cs.DataLabel dataLabel2 = new Cs.DataLabel();
            Cs.LineReference lineReference6 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference6 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference6 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference6 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor37 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation20 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset16 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor37.Append(luminanceModulation20);
            schemeColor37.Append(luminanceOffset16);

            fontReference6.Append(schemeColor37);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType4 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            dataLabel2.Append(lineReference6);
            dataLabel2.Append(fillReference6);
            dataLabel2.Append(effectReference6);
            dataLabel2.Append(fontReference6);
            dataLabel2.Append(textCharacterPropertiesType4);

            Cs.DataLabelCallout dataLabelCallout1 = new Cs.DataLabelCallout();
            Cs.LineReference lineReference7 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference7 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference7 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference7 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor38 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };
            A.LuminanceModulation luminanceModulation21 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset17 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor38.Append(luminanceModulation21);
            schemeColor38.Append(luminanceOffset17);

            fontReference7.Append(schemeColor38);

            Cs.ShapeProperties shapeProperties13 = new Cs.ShapeProperties();

            A.SolidFill solidFill28 = new A.SolidFill();
            A.SchemeColor schemeColor39 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill28.Append(schemeColor39);

            A.Outline outline17 = new A.Outline();

            A.SolidFill solidFill29 = new A.SolidFill();

            A.SchemeColor schemeColor40 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };
            A.LuminanceModulation luminanceModulation22 = new A.LuminanceModulation() { Val = 25000 };
            A.LuminanceOffset luminanceOffset18 = new A.LuminanceOffset() { Val = 75000 };

            schemeColor40.Append(luminanceModulation22);
            schemeColor40.Append(luminanceOffset18);

            solidFill29.Append(schemeColor40);

            outline17.Append(solidFill29);

            shapeProperties13.Append(solidFill28);
            shapeProperties13.Append(outline17);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType5 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            Cs.TextBodyProperties textBodyProperties1 = new Cs.TextBodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Clip, HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 36576, TopInset = 18288, RightInset = 36576, BottomInset = 18288, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit2 = new A.ShapeAutoFit();

            textBodyProperties1.Append(shapeAutoFit2);

            dataLabelCallout1.Append(lineReference7);
            dataLabelCallout1.Append(fillReference7);
            dataLabelCallout1.Append(effectReference7);
            dataLabelCallout1.Append(fontReference7);
            dataLabelCallout1.Append(shapeProperties13);
            dataLabelCallout1.Append(textCharacterPropertiesType5);
            dataLabelCallout1.Append(textBodyProperties1);

            Cs.DataPoint dataPoint4 = new Cs.DataPoint();
            Cs.LineReference lineReference8 = new Cs.LineReference() { Index = (UInt32Value)0U };

            Cs.FillReference fillReference8 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.StyleColor styleColor1 = new Cs.StyleColor() { Val = "auto" };

            fillReference8.Append(styleColor1);
            Cs.EffectReference effectReference8 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference8 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor41 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference8.Append(schemeColor41);

            Cs.ShapeProperties shapeProperties14 = new Cs.ShapeProperties();

            A.Outline outline18 = new A.Outline() { Width = 19050 };

            A.SolidFill solidFill30 = new A.SolidFill();
            A.SchemeColor schemeColor42 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill30.Append(schemeColor42);

            outline18.Append(solidFill30);

            shapeProperties14.Append(outline18);

            dataPoint4.Append(lineReference8);
            dataPoint4.Append(fillReference8);
            dataPoint4.Append(effectReference8);
            dataPoint4.Append(fontReference8);
            dataPoint4.Append(shapeProperties14);

            Cs.DataPoint3D dataPoint3D1 = new Cs.DataPoint3D();
            Cs.LineReference lineReference9 = new Cs.LineReference() { Index = (UInt32Value)0U };

            Cs.FillReference fillReference9 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.StyleColor styleColor2 = new Cs.StyleColor() { Val = "auto" };

            fillReference9.Append(styleColor2);
            Cs.EffectReference effectReference9 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference9 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor43 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference9.Append(schemeColor43);

            Cs.ShapeProperties shapeProperties15 = new Cs.ShapeProperties();

            A.Outline outline19 = new A.Outline() { Width = 25400 };

            A.SolidFill solidFill31 = new A.SolidFill();
            A.SchemeColor schemeColor44 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill31.Append(schemeColor44);

            outline19.Append(solidFill31);

            shapeProperties15.Append(outline19);

            dataPoint3D1.Append(lineReference9);
            dataPoint3D1.Append(fillReference9);
            dataPoint3D1.Append(effectReference9);
            dataPoint3D1.Append(fontReference9);
            dataPoint3D1.Append(shapeProperties15);

            Cs.DataPointLine dataPointLine1 = new Cs.DataPointLine();

            Cs.LineReference lineReference10 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor3 = new Cs.StyleColor() { Val = "auto" };

            lineReference10.Append(styleColor3);
            Cs.FillReference fillReference10 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference10 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference10 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor45 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference10.Append(schemeColor45);

            Cs.ShapeProperties shapeProperties16 = new Cs.ShapeProperties();

            A.Outline outline20 = new A.Outline() { Width = 28575, CapType = A.LineCapValues.Round };

            A.SolidFill solidFill32 = new A.SolidFill();
            A.SchemeColor schemeColor46 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill32.Append(schemeColor46);
            A.Round round4 = new A.Round();

            outline20.Append(solidFill32);
            outline20.Append(round4);

            shapeProperties16.Append(outline20);

            dataPointLine1.Append(lineReference10);
            dataPointLine1.Append(fillReference10);
            dataPointLine1.Append(effectReference10);
            dataPointLine1.Append(fontReference10);
            dataPointLine1.Append(shapeProperties16);

            Cs.DataPointMarker dataPointMarker1 = new Cs.DataPointMarker();

            Cs.LineReference lineReference11 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor4 = new Cs.StyleColor() { Val = "auto" };

            lineReference11.Append(styleColor4);

            Cs.FillReference fillReference11 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.StyleColor styleColor5 = new Cs.StyleColor() { Val = "auto" };

            fillReference11.Append(styleColor5);
            Cs.EffectReference effectReference11 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference11 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor47 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference11.Append(schemeColor47);

            Cs.ShapeProperties shapeProperties17 = new Cs.ShapeProperties();

            A.Outline outline21 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill33 = new A.SolidFill();
            A.SchemeColor schemeColor48 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill33.Append(schemeColor48);

            outline21.Append(solidFill33);

            shapeProperties17.Append(outline21);

            dataPointMarker1.Append(lineReference11);
            dataPointMarker1.Append(fillReference11);
            dataPointMarker1.Append(effectReference11);
            dataPointMarker1.Append(fontReference11);
            dataPointMarker1.Append(shapeProperties17);
            Cs.MarkerLayoutProperties markerLayoutProperties1 = new Cs.MarkerLayoutProperties() { Symbol = Cs.MarkerStyle.Circle, Size = 5 };

            Cs.DataPointWireframe dataPointWireframe1 = new Cs.DataPointWireframe();

            Cs.LineReference lineReference12 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor6 = new Cs.StyleColor() { Val = "auto" };

            lineReference12.Append(styleColor6);
            Cs.FillReference fillReference12 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference12 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference12 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor49 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference12.Append(schemeColor49);

            Cs.ShapeProperties shapeProperties18 = new Cs.ShapeProperties();

            A.Outline outline22 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Round };

            A.SolidFill solidFill34 = new A.SolidFill();
            A.SchemeColor schemeColor50 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill34.Append(schemeColor50);
            A.Round round5 = new A.Round();

            outline22.Append(solidFill34);
            outline22.Append(round5);

            shapeProperties18.Append(outline22);

            dataPointWireframe1.Append(lineReference12);
            dataPointWireframe1.Append(fillReference12);
            dataPointWireframe1.Append(effectReference12);
            dataPointWireframe1.Append(fontReference12);
            dataPointWireframe1.Append(shapeProperties18);

            Cs.DataTableStyle dataTableStyle1 = new Cs.DataTableStyle();
            Cs.LineReference lineReference13 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference13 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference13 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference13 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor51 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation23 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset19 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor51.Append(luminanceModulation23);
            schemeColor51.Append(luminanceOffset19);

            fontReference13.Append(schemeColor51);

            Cs.ShapeProperties shapeProperties19 = new Cs.ShapeProperties();
            A.NoFill noFill9 = new A.NoFill();

            A.Outline outline23 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill35 = new A.SolidFill();

            A.SchemeColor schemeColor52 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation24 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset20 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor52.Append(luminanceModulation24);
            schemeColor52.Append(luminanceOffset20);

            solidFill35.Append(schemeColor52);
            A.Round round6 = new A.Round();

            outline23.Append(solidFill35);
            outline23.Append(round6);

            shapeProperties19.Append(noFill9);
            shapeProperties19.Append(outline23);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType6 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            dataTableStyle1.Append(lineReference13);
            dataTableStyle1.Append(fillReference13);
            dataTableStyle1.Append(effectReference13);
            dataTableStyle1.Append(fontReference13);
            dataTableStyle1.Append(shapeProperties19);
            dataTableStyle1.Append(textCharacterPropertiesType6);

            Cs.DownBar downBar1 = new Cs.DownBar();
            Cs.LineReference lineReference14 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference14 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference14 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference14 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor53 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference14.Append(schemeColor53);

            Cs.ShapeProperties shapeProperties20 = new Cs.ShapeProperties();

            A.SolidFill solidFill36 = new A.SolidFill();

            A.SchemeColor schemeColor54 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };
            A.LuminanceModulation luminanceModulation25 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset21 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor54.Append(luminanceModulation25);
            schemeColor54.Append(luminanceOffset21);

            solidFill36.Append(schemeColor54);

            A.Outline outline24 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill37 = new A.SolidFill();

            A.SchemeColor schemeColor55 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation26 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset22 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor55.Append(luminanceModulation26);
            schemeColor55.Append(luminanceOffset22);

            solidFill37.Append(schemeColor55);
            A.Round round7 = new A.Round();

            outline24.Append(solidFill37);
            outline24.Append(round7);

            shapeProperties20.Append(solidFill36);
            shapeProperties20.Append(outline24);

            downBar1.Append(lineReference14);
            downBar1.Append(fillReference14);
            downBar1.Append(effectReference14);
            downBar1.Append(fontReference14);
            downBar1.Append(shapeProperties20);

            Cs.DropLine dropLine1 = new Cs.DropLine();
            Cs.LineReference lineReference15 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference15 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference15 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference15 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor56 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference15.Append(schemeColor56);

            Cs.ShapeProperties shapeProperties21 = new Cs.ShapeProperties();

            A.Outline outline25 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill38 = new A.SolidFill();

            A.SchemeColor schemeColor57 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation27 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset23 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor57.Append(luminanceModulation27);
            schemeColor57.Append(luminanceOffset23);

            solidFill38.Append(schemeColor57);
            A.Round round8 = new A.Round();

            outline25.Append(solidFill38);
            outline25.Append(round8);

            shapeProperties21.Append(outline25);

            dropLine1.Append(lineReference15);
            dropLine1.Append(fillReference15);
            dropLine1.Append(effectReference15);
            dropLine1.Append(fontReference15);
            dropLine1.Append(shapeProperties21);

            Cs.ErrorBar errorBar1 = new Cs.ErrorBar();
            Cs.LineReference lineReference16 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference16 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference16 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference16 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor58 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference16.Append(schemeColor58);

            Cs.ShapeProperties shapeProperties22 = new Cs.ShapeProperties();

            A.Outline outline26 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill39 = new A.SolidFill();

            A.SchemeColor schemeColor59 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation28 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset24 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor59.Append(luminanceModulation28);
            schemeColor59.Append(luminanceOffset24);

            solidFill39.Append(schemeColor59);
            A.Round round9 = new A.Round();

            outline26.Append(solidFill39);
            outline26.Append(round9);

            shapeProperties22.Append(outline26);

            errorBar1.Append(lineReference16);
            errorBar1.Append(fillReference16);
            errorBar1.Append(effectReference16);
            errorBar1.Append(fontReference16);
            errorBar1.Append(shapeProperties22);

            Cs.Floor floor1 = new Cs.Floor();
            Cs.LineReference lineReference17 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference17 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference17 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference17 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor60 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference17.Append(schemeColor60);

            Cs.ShapeProperties shapeProperties23 = new Cs.ShapeProperties();
            A.NoFill noFill10 = new A.NoFill();

            A.Outline outline27 = new A.Outline();
            A.NoFill noFill11 = new A.NoFill();

            outline27.Append(noFill11);

            shapeProperties23.Append(noFill10);
            shapeProperties23.Append(outline27);

            floor1.Append(lineReference17);
            floor1.Append(fillReference17);
            floor1.Append(effectReference17);
            floor1.Append(fontReference17);
            floor1.Append(shapeProperties23);

            Cs.GridlineMajor gridlineMajor1 = new Cs.GridlineMajor();
            Cs.LineReference lineReference18 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference18 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference18 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference18 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor61 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference18.Append(schemeColor61);

            Cs.ShapeProperties shapeProperties24 = new Cs.ShapeProperties();

            A.Outline outline28 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill40 = new A.SolidFill();

            A.SchemeColor schemeColor62 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation29 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset25 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor62.Append(luminanceModulation29);
            schemeColor62.Append(luminanceOffset25);

            solidFill40.Append(schemeColor62);
            A.Round round10 = new A.Round();

            outline28.Append(solidFill40);
            outline28.Append(round10);

            shapeProperties24.Append(outline28);

            gridlineMajor1.Append(lineReference18);
            gridlineMajor1.Append(fillReference18);
            gridlineMajor1.Append(effectReference18);
            gridlineMajor1.Append(fontReference18);
            gridlineMajor1.Append(shapeProperties24);

            Cs.GridlineMinor gridlineMinor1 = new Cs.GridlineMinor();
            Cs.LineReference lineReference19 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference19 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference19 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference19 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor63 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference19.Append(schemeColor63);

            Cs.ShapeProperties shapeProperties25 = new Cs.ShapeProperties();

            A.Outline outline29 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill41 = new A.SolidFill();

            A.SchemeColor schemeColor64 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation30 = new A.LuminanceModulation() { Val = 5000 };
            A.LuminanceOffset luminanceOffset26 = new A.LuminanceOffset() { Val = 95000 };

            schemeColor64.Append(luminanceModulation30);
            schemeColor64.Append(luminanceOffset26);

            solidFill41.Append(schemeColor64);
            A.Round round11 = new A.Round();

            outline29.Append(solidFill41);
            outline29.Append(round11);

            shapeProperties25.Append(outline29);

            gridlineMinor1.Append(lineReference19);
            gridlineMinor1.Append(fillReference19);
            gridlineMinor1.Append(effectReference19);
            gridlineMinor1.Append(fontReference19);
            gridlineMinor1.Append(shapeProperties25);

            Cs.HiLoLine hiLoLine1 = new Cs.HiLoLine();
            Cs.LineReference lineReference20 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference20 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference20 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference20 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor65 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference20.Append(schemeColor65);

            Cs.ShapeProperties shapeProperties26 = new Cs.ShapeProperties();

            A.Outline outline30 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill42 = new A.SolidFill();

            A.SchemeColor schemeColor66 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation31 = new A.LuminanceModulation() { Val = 50000 };
            A.LuminanceOffset luminanceOffset27 = new A.LuminanceOffset() { Val = 50000 };

            schemeColor66.Append(luminanceModulation31);
            schemeColor66.Append(luminanceOffset27);

            solidFill42.Append(schemeColor66);
            A.Round round12 = new A.Round();

            outline30.Append(solidFill42);
            outline30.Append(round12);

            shapeProperties26.Append(outline30);

            hiLoLine1.Append(lineReference20);
            hiLoLine1.Append(fillReference20);
            hiLoLine1.Append(effectReference20);
            hiLoLine1.Append(fontReference20);
            hiLoLine1.Append(shapeProperties26);

            Cs.LeaderLine leaderLine1 = new Cs.LeaderLine();
            Cs.LineReference lineReference21 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference21 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference21 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference21 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor67 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference21.Append(schemeColor67);

            Cs.ShapeProperties shapeProperties27 = new Cs.ShapeProperties();

            A.Outline outline31 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill43 = new A.SolidFill();

            A.SchemeColor schemeColor68 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation32 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset28 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor68.Append(luminanceModulation32);
            schemeColor68.Append(luminanceOffset28);

            solidFill43.Append(schemeColor68);
            A.Round round13 = new A.Round();

            outline31.Append(solidFill43);
            outline31.Append(round13);

            shapeProperties27.Append(outline31);

            leaderLine1.Append(lineReference21);
            leaderLine1.Append(fillReference21);
            leaderLine1.Append(effectReference21);
            leaderLine1.Append(fontReference21);
            leaderLine1.Append(shapeProperties27);

            Cs.LegendStyle legendStyle1 = new Cs.LegendStyle();
            Cs.LineReference lineReference22 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference22 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference22 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference22 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor69 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation33 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset29 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor69.Append(luminanceModulation33);
            schemeColor69.Append(luminanceOffset29);

            fontReference22.Append(schemeColor69);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType7 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            legendStyle1.Append(lineReference22);
            legendStyle1.Append(fillReference22);
            legendStyle1.Append(effectReference22);
            legendStyle1.Append(fontReference22);
            legendStyle1.Append(textCharacterPropertiesType7);

            Cs.PlotArea plotArea2 = new Cs.PlotArea() { Modifiers = new ListValue<StringValue>() { InnerText = "allowNoFillOverride allowNoLineOverride" } };
            Cs.LineReference lineReference23 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference23 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference23 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference23 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor70 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference23.Append(schemeColor70);

            plotArea2.Append(lineReference23);
            plotArea2.Append(fillReference23);
            plotArea2.Append(effectReference23);
            plotArea2.Append(fontReference23);

            Cs.PlotArea3D plotArea3D1 = new Cs.PlotArea3D() { Modifiers = new ListValue<StringValue>() { InnerText = "allowNoFillOverride allowNoLineOverride" } };
            Cs.LineReference lineReference24 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference24 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference24 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference24 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor71 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference24.Append(schemeColor71);

            plotArea3D1.Append(lineReference24);
            plotArea3D1.Append(fillReference24);
            plotArea3D1.Append(effectReference24);
            plotArea3D1.Append(fontReference24);

            Cs.SeriesAxis seriesAxis1 = new Cs.SeriesAxis();
            Cs.LineReference lineReference25 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference25 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference25 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference25 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor72 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation34 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset30 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor72.Append(luminanceModulation34);
            schemeColor72.Append(luminanceOffset30);

            fontReference25.Append(schemeColor72);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType8 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            seriesAxis1.Append(lineReference25);
            seriesAxis1.Append(fillReference25);
            seriesAxis1.Append(effectReference25);
            seriesAxis1.Append(fontReference25);
            seriesAxis1.Append(textCharacterPropertiesType8);

            Cs.SeriesLine seriesLine1 = new Cs.SeriesLine();
            Cs.LineReference lineReference26 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference26 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference26 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference26 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor73 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference26.Append(schemeColor73);

            Cs.ShapeProperties shapeProperties28 = new Cs.ShapeProperties();

            A.Outline outline32 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill44 = new A.SolidFill();

            A.SchemeColor schemeColor74 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation35 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset31 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor74.Append(luminanceModulation35);
            schemeColor74.Append(luminanceOffset31);

            solidFill44.Append(schemeColor74);
            A.Round round14 = new A.Round();

            outline32.Append(solidFill44);
            outline32.Append(round14);

            shapeProperties28.Append(outline32);

            seriesLine1.Append(lineReference26);
            seriesLine1.Append(fillReference26);
            seriesLine1.Append(effectReference26);
            seriesLine1.Append(fontReference26);
            seriesLine1.Append(shapeProperties28);

            Cs.TitleStyle titleStyle1 = new Cs.TitleStyle();
            Cs.LineReference lineReference27 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference27 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference27 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference27 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor75 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation36 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset32 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor75.Append(luminanceModulation36);
            schemeColor75.Append(luminanceOffset32);

            fontReference27.Append(schemeColor75);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType9 = new Cs.TextCharacterPropertiesType() { FontSize = 1400, Bold = false, Kerning = 1200, Spacing = 0, Baseline = 0 };

            titleStyle1.Append(lineReference27);
            titleStyle1.Append(fillReference27);
            titleStyle1.Append(effectReference27);
            titleStyle1.Append(fontReference27);
            titleStyle1.Append(textCharacterPropertiesType9);

            Cs.TrendlineStyle trendlineStyle1 = new Cs.TrendlineStyle();

            Cs.LineReference lineReference28 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor7 = new Cs.StyleColor() { Val = "auto" };

            lineReference28.Append(styleColor7);
            Cs.FillReference fillReference28 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference28 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference28 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor76 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference28.Append(schemeColor76);

            Cs.ShapeProperties shapeProperties29 = new Cs.ShapeProperties();

            A.Outline outline33 = new A.Outline() { Width = 19050, CapType = A.LineCapValues.Round };

            A.SolidFill solidFill45 = new A.SolidFill();
            A.SchemeColor schemeColor77 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill45.Append(schemeColor77);
            A.PresetDash presetDash1 = new A.PresetDash() { Val = A.PresetLineDashValues.SystemDot };

            outline33.Append(solidFill45);
            outline33.Append(presetDash1);

            shapeProperties29.Append(outline33);

            trendlineStyle1.Append(lineReference28);
            trendlineStyle1.Append(fillReference28);
            trendlineStyle1.Append(effectReference28);
            trendlineStyle1.Append(fontReference28);
            trendlineStyle1.Append(shapeProperties29);

            Cs.TrendlineLabel trendlineLabel1 = new Cs.TrendlineLabel();
            Cs.LineReference lineReference29 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference29 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference29 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference29 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor78 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation37 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset33 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor78.Append(luminanceModulation37);
            schemeColor78.Append(luminanceOffset33);

            fontReference29.Append(schemeColor78);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType10 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            trendlineLabel1.Append(lineReference29);
            trendlineLabel1.Append(fillReference29);
            trendlineLabel1.Append(effectReference29);
            trendlineLabel1.Append(fontReference29);
            trendlineLabel1.Append(textCharacterPropertiesType10);

            Cs.UpBar upBar1 = new Cs.UpBar();
            Cs.LineReference lineReference30 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference30 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference30 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference30 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor79 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference30.Append(schemeColor79);

            Cs.ShapeProperties shapeProperties30 = new Cs.ShapeProperties();

            A.SolidFill solidFill46 = new A.SolidFill();
            A.SchemeColor schemeColor80 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill46.Append(schemeColor80);

            A.Outline outline34 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill47 = new A.SolidFill();

            A.SchemeColor schemeColor81 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation38 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset34 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor81.Append(luminanceModulation38);
            schemeColor81.Append(luminanceOffset34);

            solidFill47.Append(schemeColor81);
            A.Round round15 = new A.Round();

            outline34.Append(solidFill47);
            outline34.Append(round15);

            shapeProperties30.Append(solidFill46);
            shapeProperties30.Append(outline34);

            upBar1.Append(lineReference30);
            upBar1.Append(fillReference30);
            upBar1.Append(effectReference30);
            upBar1.Append(fontReference30);
            upBar1.Append(shapeProperties30);

            Cs.ValueAxis valueAxis1 = new Cs.ValueAxis();
            Cs.LineReference lineReference31 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference31 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference31 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference31 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor82 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation39 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset35 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor82.Append(luminanceModulation39);
            schemeColor82.Append(luminanceOffset35);

            fontReference31.Append(schemeColor82);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType11 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            valueAxis1.Append(lineReference31);
            valueAxis1.Append(fillReference31);
            valueAxis1.Append(effectReference31);
            valueAxis1.Append(fontReference31);
            valueAxis1.Append(textCharacterPropertiesType11);

            Cs.Wall wall1 = new Cs.Wall();
            Cs.LineReference lineReference32 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference32 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference32 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference32 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor83 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference32.Append(schemeColor83);

            Cs.ShapeProperties shapeProperties31 = new Cs.ShapeProperties();
            A.NoFill noFill12 = new A.NoFill();

            A.Outline outline35 = new A.Outline();
            A.NoFill noFill13 = new A.NoFill();

            outline35.Append(noFill13);

            shapeProperties31.Append(noFill12);
            shapeProperties31.Append(outline35);

            wall1.Append(lineReference32);
            wall1.Append(fillReference32);
            wall1.Append(effectReference32);
            wall1.Append(fontReference32);
            wall1.Append(shapeProperties31);

            chartStyle1.Append(axisTitle1);
            chartStyle1.Append(categoryAxis1);
            chartStyle1.Append(chartArea1);
            chartStyle1.Append(dataLabel2);
            chartStyle1.Append(dataLabelCallout1);
            chartStyle1.Append(dataPoint4);
            chartStyle1.Append(dataPoint3D1);
            chartStyle1.Append(dataPointLine1);
            chartStyle1.Append(dataPointMarker1);
            chartStyle1.Append(markerLayoutProperties1);
            chartStyle1.Append(dataPointWireframe1);
            chartStyle1.Append(dataTableStyle1);
            chartStyle1.Append(downBar1);
            chartStyle1.Append(dropLine1);
            chartStyle1.Append(errorBar1);
            chartStyle1.Append(floor1);
            chartStyle1.Append(gridlineMajor1);
            chartStyle1.Append(gridlineMinor1);
            chartStyle1.Append(hiLoLine1);
            chartStyle1.Append(leaderLine1);
            chartStyle1.Append(legendStyle1);
            chartStyle1.Append(plotArea2);
            chartStyle1.Append(plotArea3D1);
            chartStyle1.Append(seriesAxis1);
            chartStyle1.Append(seriesLine1);
            chartStyle1.Append(titleStyle1);
            chartStyle1.Append(trendlineStyle1);
            chartStyle1.Append(trendlineLabel1);
            chartStyle1.Append(upBar1);
            chartStyle1.Append(valueAxis1);
            chartStyle1.Append(wall1);

            chartStylePart1.ChartStyle = chartStyle1;
        }

        // Generates content of chartPart2.
        private void GenerateChartPart2Content(ChartPart chartPart2)
        {
            C.ChartSpace chartSpace2 = new C.ChartSpace();
            chartSpace2.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
            chartSpace2.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            chartSpace2.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            chartSpace2.AddNamespaceDeclaration("c16r2", "http://schemas.microsoft.com/office/drawing/2015/06/chart");
            C.Date1904 date19042 = new C.Date1904() { Val = false };
            C.EditingLanguage editingLanguage2 = new C.EditingLanguage() { Val = "en-US" };
            C.RoundedCorners roundedCorners2 = new C.RoundedCorners() { Val = false };

            AlternateContent alternateContent3 = new AlternateContent();
            alternateContent3.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");

            AlternateContentChoice alternateContentChoice3 = new AlternateContentChoice() { Requires = "c14" };
            alternateContentChoice3.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            C14.Style style3 = new C14.Style() { Val = 102 };

            alternateContentChoice3.Append(style3);

            AlternateContentFallback alternateContentFallback2 = new AlternateContentFallback();
            C.Style style4 = new C.Style() { Val = 2 };

            alternateContentFallback2.Append(style4);

            alternateContent3.Append(alternateContentChoice3);
            alternateContent3.Append(alternateContentFallback2);

            C.PivotSource pivotSource2 = new C.PivotSource();
            C.PivotTableName pivotTableName2 = new C.PivotTableName();
            pivotTableName2.Text = "[focustest.xlsx]Cover!PivotTable8";
            C.FormatId formatId2 = new C.FormatId() { Val = (UInt32Value)8U };

            pivotSource2.Append(pivotTableName2);
            pivotSource2.Append(formatId2);

            C.Chart chart2 = new C.Chart();
            C.AutoTitleDeleted autoTitleDeleted2 = new C.AutoTitleDeleted() { Val = false };

            C.PivotFormats pivotFormats2 = new C.PivotFormats();

            C.PivotFormat pivotFormat5 = new C.PivotFormat();
            C.Index index10 = new C.Index() { Val = (UInt32Value)0U };

            C.ShapeProperties shapeProperties32 = new C.ShapeProperties();

            A.SolidFill solidFill48 = new A.SolidFill();
            A.SchemeColor schemeColor84 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent6 };

            solidFill48.Append(schemeColor84);

            A.Outline outline36 = new A.Outline();
            A.NoFill noFill14 = new A.NoFill();

            outline36.Append(noFill14);
            A.EffectList effectList13 = new A.EffectList();
            A.Shape3DType shape3DType1 = new A.Shape3DType();

            shapeProperties32.Append(solidFill48);
            shapeProperties32.Append(outline36);
            shapeProperties32.Append(effectList13);
            shapeProperties32.Append(shape3DType1);

            C.Marker marker2 = new C.Marker();
            C.Symbol symbol2 = new C.Symbol() { Val = C.MarkerStyleValues.None };

            marker2.Append(symbol2);

            C.DataLabel dataLabel3 = new C.DataLabel();
            C.Index index11 = new C.Index() { Val = (UInt32Value)0U };

            C.ChartShapeProperties chartShapeProperties7 = new C.ChartShapeProperties();
            A.NoFill noFill15 = new A.NoFill();

            A.Outline outline37 = new A.Outline();
            A.NoFill noFill16 = new A.NoFill();

            outline37.Append(noFill16);
            A.EffectList effectList14 = new A.EffectList();

            chartShapeProperties7.Append(noFill15);
            chartShapeProperties7.Append(outline37);
            chartShapeProperties7.Append(effectList14);

            C.TextProperties textProperties5 = new C.TextProperties();

            A.BodyProperties bodyProperties8 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit3 = new A.ShapeAutoFit();

            bodyProperties8.Append(shapeAutoFit3);
            A.ListStyle listStyle8 = new A.ListStyle();

            A.Paragraph paragraph8 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties6 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties6 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill49 = new A.SolidFill();

            A.SchemeColor schemeColor85 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation40 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset36 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor85.Append(luminanceModulation40);
            schemeColor85.Append(luminanceOffset36);

            solidFill49.Append(schemeColor85);
            A.LatinFont latinFont5 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont5 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont5 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties6.Append(solidFill49);
            defaultRunProperties6.Append(latinFont5);
            defaultRunProperties6.Append(eastAsianFont5);
            defaultRunProperties6.Append(complexScriptFont5);

            paragraphProperties6.Append(defaultRunProperties6);
            A.EndParagraphRunProperties endParagraphRunProperties7 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph8.Append(paragraphProperties6);
            paragraph8.Append(endParagraphRunProperties7);

            textProperties5.Append(bodyProperties8);
            textProperties5.Append(listStyle8);
            textProperties5.Append(paragraph8);
            C.ShowLegendKey showLegendKey3 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue3 = new C.ShowValue() { Val = true };
            C.ShowCategoryName showCategoryName3 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName3 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent3 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize3 = new C.ShowBubbleSize() { Val = false };

            C.DLblExtensionList dLblExtensionList2 = new C.DLblExtensionList();
            dLblExtensionList2.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            dLblExtensionList2.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            dLblExtensionList2.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            dLblExtensionList2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            dLblExtensionList2.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.DLblExtension dLblExtension2 = new C.DLblExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblExtension2.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");

            dLblExtensionList2.Append(dLblExtension2);

            dataLabel3.Append(index11);
            dataLabel3.Append(chartShapeProperties7);
            dataLabel3.Append(textProperties5);
            dataLabel3.Append(showLegendKey3);
            dataLabel3.Append(showValue3);
            dataLabel3.Append(showCategoryName3);
            dataLabel3.Append(showSeriesName3);
            dataLabel3.Append(showPercent3);
            dataLabel3.Append(showBubbleSize3);
            dataLabel3.Append(dLblExtensionList2);

            pivotFormat5.Append(index10);
            pivotFormat5.Append(shapeProperties32);
            pivotFormat5.Append(marker2);
            pivotFormat5.Append(dataLabel3);

            C.PivotFormat pivotFormat6 = new C.PivotFormat();
            C.Index index12 = new C.Index() { Val = (UInt32Value)1U };

            C.ShapeProperties shapeProperties33 = new C.ShapeProperties();

            A.SolidFill solidFill50 = new A.SolidFill();

            A.SchemeColor schemeColor86 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent2 };
            A.LuminanceModulation luminanceModulation41 = new A.LuminanceModulation() { Val = 60000 };
            A.LuminanceOffset luminanceOffset37 = new A.LuminanceOffset() { Val = 40000 };

            schemeColor86.Append(luminanceModulation41);
            schemeColor86.Append(luminanceOffset37);

            solidFill50.Append(schemeColor86);

            A.Outline outline38 = new A.Outline();
            A.NoFill noFill17 = new A.NoFill();

            outline38.Append(noFill17);
            A.EffectList effectList15 = new A.EffectList();
            A.Shape3DType shape3DType2 = new A.Shape3DType();

            shapeProperties33.Append(solidFill50);
            shapeProperties33.Append(outline38);
            shapeProperties33.Append(effectList15);
            shapeProperties33.Append(shape3DType2);

            C.Marker marker3 = new C.Marker();
            C.Symbol symbol3 = new C.Symbol() { Val = C.MarkerStyleValues.None };

            marker3.Append(symbol3);

            C.DataLabel dataLabel4 = new C.DataLabel();
            C.Index index13 = new C.Index() { Val = (UInt32Value)0U };

            C.ChartShapeProperties chartShapeProperties8 = new C.ChartShapeProperties();
            A.NoFill noFill18 = new A.NoFill();

            A.Outline outline39 = new A.Outline();
            A.NoFill noFill19 = new A.NoFill();

            outline39.Append(noFill19);
            A.EffectList effectList16 = new A.EffectList();

            chartShapeProperties8.Append(noFill18);
            chartShapeProperties8.Append(outline39);
            chartShapeProperties8.Append(effectList16);

            C.TextProperties textProperties6 = new C.TextProperties();

            A.BodyProperties bodyProperties9 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit4 = new A.ShapeAutoFit();

            bodyProperties9.Append(shapeAutoFit4);
            A.ListStyle listStyle9 = new A.ListStyle();

            A.Paragraph paragraph9 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties7 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties7 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill51 = new A.SolidFill();

            A.SchemeColor schemeColor87 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation42 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset38 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor87.Append(luminanceModulation42);
            schemeColor87.Append(luminanceOffset38);

            solidFill51.Append(schemeColor87);
            A.LatinFont latinFont6 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont6 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont6 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties7.Append(solidFill51);
            defaultRunProperties7.Append(latinFont6);
            defaultRunProperties7.Append(eastAsianFont6);
            defaultRunProperties7.Append(complexScriptFont6);

            paragraphProperties7.Append(defaultRunProperties7);
            A.EndParagraphRunProperties endParagraphRunProperties8 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph9.Append(paragraphProperties7);
            paragraph9.Append(endParagraphRunProperties8);

            textProperties6.Append(bodyProperties9);
            textProperties6.Append(listStyle9);
            textProperties6.Append(paragraph9);
            C.ShowLegendKey showLegendKey4 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue4 = new C.ShowValue() { Val = true };
            C.ShowCategoryName showCategoryName4 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName4 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent4 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize4 = new C.ShowBubbleSize() { Val = false };

            C.DLblExtensionList dLblExtensionList3 = new C.DLblExtensionList();
            dLblExtensionList3.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            dLblExtensionList3.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            dLblExtensionList3.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            dLblExtensionList3.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            dLblExtensionList3.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.DLblExtension dLblExtension3 = new C.DLblExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblExtension3.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");

            dLblExtensionList3.Append(dLblExtension3);

            dataLabel4.Append(index13);
            dataLabel4.Append(chartShapeProperties8);
            dataLabel4.Append(textProperties6);
            dataLabel4.Append(showLegendKey4);
            dataLabel4.Append(showValue4);
            dataLabel4.Append(showCategoryName4);
            dataLabel4.Append(showSeriesName4);
            dataLabel4.Append(showPercent4);
            dataLabel4.Append(showBubbleSize4);
            dataLabel4.Append(dLblExtensionList3);

            pivotFormat6.Append(index12);
            pivotFormat6.Append(shapeProperties33);
            pivotFormat6.Append(marker3);
            pivotFormat6.Append(dataLabel4);

            C.PivotFormat pivotFormat7 = new C.PivotFormat();
            C.Index index14 = new C.Index() { Val = (UInt32Value)2U };

            C.ShapeProperties shapeProperties34 = new C.ShapeProperties();

            A.SolidFill solidFill52 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex3 = new A.RgbColorModelHex() { Val = "FF4B4B" };

            solidFill52.Append(rgbColorModelHex3);

            A.Outline outline40 = new A.Outline();
            A.NoFill noFill20 = new A.NoFill();

            outline40.Append(noFill20);
            A.EffectList effectList17 = new A.EffectList();
            A.Shape3DType shape3DType3 = new A.Shape3DType();

            shapeProperties34.Append(solidFill52);
            shapeProperties34.Append(outline40);
            shapeProperties34.Append(effectList17);
            shapeProperties34.Append(shape3DType3);

            C.Marker marker4 = new C.Marker();
            C.Symbol symbol4 = new C.Symbol() { Val = C.MarkerStyleValues.None };

            marker4.Append(symbol4);

            C.DataLabel dataLabel5 = new C.DataLabel();
            C.Index index15 = new C.Index() { Val = (UInt32Value)0U };

            C.ChartShapeProperties chartShapeProperties9 = new C.ChartShapeProperties();
            A.NoFill noFill21 = new A.NoFill();

            A.Outline outline41 = new A.Outline();
            A.NoFill noFill22 = new A.NoFill();

            outline41.Append(noFill22);
            A.EffectList effectList18 = new A.EffectList();

            chartShapeProperties9.Append(noFill21);
            chartShapeProperties9.Append(outline41);
            chartShapeProperties9.Append(effectList18);

            C.TextProperties textProperties7 = new C.TextProperties();

            A.BodyProperties bodyProperties10 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit5 = new A.ShapeAutoFit();

            bodyProperties10.Append(shapeAutoFit5);
            A.ListStyle listStyle10 = new A.ListStyle();

            A.Paragraph paragraph10 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties8 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties8 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill53 = new A.SolidFill();

            A.SchemeColor schemeColor88 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation43 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset39 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor88.Append(luminanceModulation43);
            schemeColor88.Append(luminanceOffset39);

            solidFill53.Append(schemeColor88);
            A.LatinFont latinFont7 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont7 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont7 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties8.Append(solidFill53);
            defaultRunProperties8.Append(latinFont7);
            defaultRunProperties8.Append(eastAsianFont7);
            defaultRunProperties8.Append(complexScriptFont7);

            paragraphProperties8.Append(defaultRunProperties8);
            A.EndParagraphRunProperties endParagraphRunProperties9 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph10.Append(paragraphProperties8);
            paragraph10.Append(endParagraphRunProperties9);

            textProperties7.Append(bodyProperties10);
            textProperties7.Append(listStyle10);
            textProperties7.Append(paragraph10);
            C.ShowLegendKey showLegendKey5 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue5 = new C.ShowValue() { Val = true };
            C.ShowCategoryName showCategoryName5 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName5 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent5 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize5 = new C.ShowBubbleSize() { Val = false };

            C.DLblExtensionList dLblExtensionList4 = new C.DLblExtensionList();
            dLblExtensionList4.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            dLblExtensionList4.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            dLblExtensionList4.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            dLblExtensionList4.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            dLblExtensionList4.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.DLblExtension dLblExtension4 = new C.DLblExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblExtension4.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");

            dLblExtensionList4.Append(dLblExtension4);

            dataLabel5.Append(index15);
            dataLabel5.Append(chartShapeProperties9);
            dataLabel5.Append(textProperties7);
            dataLabel5.Append(showLegendKey5);
            dataLabel5.Append(showValue5);
            dataLabel5.Append(showCategoryName5);
            dataLabel5.Append(showSeriesName5);
            dataLabel5.Append(showPercent5);
            dataLabel5.Append(showBubbleSize5);
            dataLabel5.Append(dLblExtensionList4);

            pivotFormat7.Append(index14);
            pivotFormat7.Append(shapeProperties34);
            pivotFormat7.Append(marker4);
            pivotFormat7.Append(dataLabel5);

            pivotFormats2.Append(pivotFormat5);
            pivotFormats2.Append(pivotFormat6);
            pivotFormats2.Append(pivotFormat7);

            C.View3D view3D1 = new C.View3D();
            C.RotateX rotateX1 = new C.RotateX() { Val = 15 };
            C.RotateY rotateY1 = new C.RotateY() { Val = (UInt16Value)20U };
            C.DepthPercent depthPercent1 = new C.DepthPercent() { Val = (UInt16Value)100U };
            C.RightAngleAxes rightAngleAxes1 = new C.RightAngleAxes() { Val = true };

            view3D1.Append(rotateX1);
            view3D1.Append(rotateY1);
            view3D1.Append(depthPercent1);
            view3D1.Append(rightAngleAxes1);

            C.Floor floor2 = new C.Floor();
            C.Thickness thickness1 = new C.Thickness() { Val = 0 };

            C.ShapeProperties shapeProperties35 = new C.ShapeProperties();
            A.NoFill noFill23 = new A.NoFill();

            A.Outline outline42 = new A.Outline();
            A.NoFill noFill24 = new A.NoFill();

            outline42.Append(noFill24);
            A.EffectList effectList19 = new A.EffectList();
            A.Shape3DType shape3DType4 = new A.Shape3DType();

            shapeProperties35.Append(noFill23);
            shapeProperties35.Append(outline42);
            shapeProperties35.Append(effectList19);
            shapeProperties35.Append(shape3DType4);

            floor2.Append(thickness1);
            floor2.Append(shapeProperties35);

            C.SideWall sideWall1 = new C.SideWall();
            C.Thickness thickness2 = new C.Thickness() { Val = 0 };

            C.ShapeProperties shapeProperties36 = new C.ShapeProperties();
            A.NoFill noFill25 = new A.NoFill();

            A.Outline outline43 = new A.Outline();
            A.NoFill noFill26 = new A.NoFill();

            outline43.Append(noFill26);
            A.EffectList effectList20 = new A.EffectList();
            A.Shape3DType shape3DType5 = new A.Shape3DType();

            shapeProperties36.Append(noFill25);
            shapeProperties36.Append(outline43);
            shapeProperties36.Append(effectList20);
            shapeProperties36.Append(shape3DType5);

            sideWall1.Append(thickness2);
            sideWall1.Append(shapeProperties36);

            C.BackWall backWall1 = new C.BackWall();
            C.Thickness thickness3 = new C.Thickness() { Val = 0 };

            C.ShapeProperties shapeProperties37 = new C.ShapeProperties();
            A.NoFill noFill27 = new A.NoFill();

            A.Outline outline44 = new A.Outline();
            A.NoFill noFill28 = new A.NoFill();

            outline44.Append(noFill28);
            A.EffectList effectList21 = new A.EffectList();
            A.Shape3DType shape3DType6 = new A.Shape3DType();

            shapeProperties37.Append(noFill27);
            shapeProperties37.Append(outline44);
            shapeProperties37.Append(effectList21);
            shapeProperties37.Append(shape3DType6);

            backWall1.Append(thickness3);
            backWall1.Append(shapeProperties37);

            C.PlotArea plotArea3 = new C.PlotArea();
            C.Layout layout2 = new C.Layout();

            C.Bar3DChart bar3DChart1 = new C.Bar3DChart();
            C.BarDirection barDirection1 = new C.BarDirection() { Val = C.BarDirectionValues.Column };
            C.BarGrouping barGrouping1 = new C.BarGrouping() { Val = C.BarGroupingValues.Clustered };
            C.VaryColors varyColors2 = new C.VaryColors() { Val = false };

            C.BarChartSeries barChartSeries1 = new C.BarChartSeries();
            C.Index index16 = new C.Index() { Val = (UInt32Value)0U };
            C.Order order2 = new C.Order() { Val = (UInt32Value)0U };

            C.SeriesText seriesText2 = new C.SeriesText();

            C.StringReference stringReference3 = new C.StringReference();
            C.Formula formula4 = new C.Formula();
            formula4.Text = "Cover!$B$33:$B$34";

            C.StringCache stringCache3 = new C.StringCache();
            C.PointCount pointCount4 = new C.PointCount() { Val = (UInt32Value)1U };

            C.StringPoint stringPoint5 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue8 = new C.NumericValue();
            numericValue8.Text = "Completed";

            stringPoint5.Append(numericValue8);

            stringCache3.Append(pointCount4);
            stringCache3.Append(stringPoint5);

            stringReference3.Append(formula4);
            stringReference3.Append(stringCache3);

            seriesText2.Append(stringReference3);

            C.ChartShapeProperties chartShapeProperties10 = new C.ChartShapeProperties();

            A.SolidFill solidFill54 = new A.SolidFill();
            A.SchemeColor schemeColor89 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent6 };

            solidFill54.Append(schemeColor89);

            A.Outline outline45 = new A.Outline();
            A.NoFill noFill29 = new A.NoFill();

            outline45.Append(noFill29);
            A.EffectList effectList22 = new A.EffectList();
            A.Shape3DType shape3DType7 = new A.Shape3DType();

            chartShapeProperties10.Append(solidFill54);
            chartShapeProperties10.Append(outline45);
            chartShapeProperties10.Append(effectList22);
            chartShapeProperties10.Append(shape3DType7);
            C.InvertIfNegative invertIfNegative1 = new C.InvertIfNegative() { Val = false };

            C.DataLabels dataLabels2 = new C.DataLabels();

            C.ChartShapeProperties chartShapeProperties11 = new C.ChartShapeProperties();
            A.NoFill noFill30 = new A.NoFill();

            A.Outline outline46 = new A.Outline();
            A.NoFill noFill31 = new A.NoFill();

            outline46.Append(noFill31);
            A.EffectList effectList23 = new A.EffectList();

            chartShapeProperties11.Append(noFill30);
            chartShapeProperties11.Append(outline46);
            chartShapeProperties11.Append(effectList23);

            C.TextProperties textProperties8 = new C.TextProperties();

            A.BodyProperties bodyProperties11 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit6 = new A.ShapeAutoFit();

            bodyProperties11.Append(shapeAutoFit6);
            A.ListStyle listStyle11 = new A.ListStyle();

            A.Paragraph paragraph11 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties9 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties9 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill55 = new A.SolidFill();

            A.SchemeColor schemeColor90 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation44 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset40 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor90.Append(luminanceModulation44);
            schemeColor90.Append(luminanceOffset40);

            solidFill55.Append(schemeColor90);
            A.LatinFont latinFont8 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont8 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont8 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties9.Append(solidFill55);
            defaultRunProperties9.Append(latinFont8);
            defaultRunProperties9.Append(eastAsianFont8);
            defaultRunProperties9.Append(complexScriptFont8);

            paragraphProperties9.Append(defaultRunProperties9);
            A.EndParagraphRunProperties endParagraphRunProperties10 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph11.Append(paragraphProperties9);
            paragraph11.Append(endParagraphRunProperties10);

            textProperties8.Append(bodyProperties11);
            textProperties8.Append(listStyle11);
            textProperties8.Append(paragraph11);
            C.ShowLegendKey showLegendKey6 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue6 = new C.ShowValue() { Val = true };
            C.ShowCategoryName showCategoryName6 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName6 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent6 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize6 = new C.ShowBubbleSize() { Val = false };
            C.ShowLeaderLines showLeaderLines2 = new C.ShowLeaderLines() { Val = false };

            C.DLblsExtensionList dLblsExtensionList1 = new C.DLblsExtensionList();

            C.DLblsExtension dLblsExtension1 = new C.DLblsExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblsExtension1.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            C15.ShowLeaderLines showLeaderLines3 = new C15.ShowLeaderLines() { Val = true };

            C15.LeaderLines leaderLines1 = new C15.LeaderLines();

            C.ChartShapeProperties chartShapeProperties12 = new C.ChartShapeProperties();

            A.Outline outline47 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill56 = new A.SolidFill();

            A.SchemeColor schemeColor91 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation45 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset41 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor91.Append(luminanceModulation45);
            schemeColor91.Append(luminanceOffset41);

            solidFill56.Append(schemeColor91);
            A.Round round16 = new A.Round();

            outline47.Append(solidFill56);
            outline47.Append(round16);
            A.EffectList effectList24 = new A.EffectList();

            chartShapeProperties12.Append(outline47);
            chartShapeProperties12.Append(effectList24);

            leaderLines1.Append(chartShapeProperties12);

            dLblsExtension1.Append(showLeaderLines3);
            dLblsExtension1.Append(leaderLines1);

            dLblsExtensionList1.Append(dLblsExtension1);

            dataLabels2.Append(chartShapeProperties11);
            dataLabels2.Append(textProperties8);
            dataLabels2.Append(showLegendKey6);
            dataLabels2.Append(showValue6);
            dataLabels2.Append(showCategoryName6);
            dataLabels2.Append(showSeriesName6);
            dataLabels2.Append(showPercent6);
            dataLabels2.Append(showBubbleSize6);
            dataLabels2.Append(showLeaderLines2);
            dataLabels2.Append(dLblsExtensionList1);

            C.CategoryAxisData categoryAxisData2 = new C.CategoryAxisData();

            C.StringReference stringReference4 = new C.StringReference();
            C.Formula formula5 = new C.Formula();
            formula5.Text = "Cover!$A$35:$A$38";

            C.StringCache stringCache4 = new C.StringCache();
            C.PointCount pointCount5 = new C.PointCount() { Val = (UInt32Value)3U };

            C.StringPoint stringPoint6 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue9 = new C.NumericValue();
            numericValue9.Text = "ChemCo Distilling Visit";

            stringPoint6.Append(numericValue9);

            C.StringPoint stringPoint7 = new C.StringPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue10 = new C.NumericValue();
            numericValue10.Text = "Focus Bucket";

            stringPoint7.Append(numericValue10);

            C.StringPoint stringPoint8 = new C.StringPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue11 = new C.NumericValue();
            numericValue11.Text = "Redmond Site Visit";

            stringPoint8.Append(numericValue11);

            stringCache4.Append(pointCount5);
            stringCache4.Append(stringPoint6);
            stringCache4.Append(stringPoint7);
            stringCache4.Append(stringPoint8);

            stringReference4.Append(formula5);
            stringReference4.Append(stringCache4);

            categoryAxisData2.Append(stringReference4);

            C.Values values2 = new C.Values();

            C.NumberReference numberReference2 = new C.NumberReference();
            C.Formula formula6 = new C.Formula();
            formula6.Text = "Cover!$B$35:$B$38";

            C.NumberingCache numberingCache2 = new C.NumberingCache();
            C.FormatCode formatCode2 = new C.FormatCode();
            formatCode2.Text = "General";
            C.PointCount pointCount6 = new C.PointCount() { Val = (UInt32Value)3U };

            C.NumericPoint numericPoint4 = new C.NumericPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue12 = new C.NumericValue();
            numericValue12.Text = "1";

            numericPoint4.Append(numericValue12);

            numberingCache2.Append(formatCode2);
            numberingCache2.Append(pointCount6);
            numberingCache2.Append(numericPoint4);

            numberReference2.Append(formula6);
            numberReference2.Append(numberingCache2);

            values2.Append(numberReference2);

            C.BarSerExtensionList barSerExtensionList1 = new C.BarSerExtensionList();
            barSerExtensionList1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            barSerExtensionList1.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            barSerExtensionList1.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            barSerExtensionList1.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            barSerExtensionList1.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.BarSerExtension barSerExtension1 = new C.BarSerExtension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            barSerExtension1.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement18 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000000-B1BC-48BA-8EDE-BB1B182C84D9}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            barSerExtension1.Append(openXmlUnknownElement18);

            barSerExtensionList1.Append(barSerExtension1);

            barChartSeries1.Append(index16);
            barChartSeries1.Append(order2);
            barChartSeries1.Append(seriesText2);
            barChartSeries1.Append(chartShapeProperties10);
            barChartSeries1.Append(invertIfNegative1);
            barChartSeries1.Append(dataLabels2);
            barChartSeries1.Append(categoryAxisData2);
            barChartSeries1.Append(values2);
            barChartSeries1.Append(barSerExtensionList1);

            C.BarChartSeries barChartSeries2 = new C.BarChartSeries();
            C.Index index17 = new C.Index() { Val = (UInt32Value)1U };
            C.Order order3 = new C.Order() { Val = (UInt32Value)1U };

            C.SeriesText seriesText3 = new C.SeriesText();

            C.StringReference stringReference5 = new C.StringReference();
            C.Formula formula7 = new C.Formula();
            formula7.Text = "Cover!$C$33:$C$34";

            C.StringCache stringCache5 = new C.StringCache();
            C.PointCount pointCount7 = new C.PointCount() { Val = (UInt32Value)1U };

            C.StringPoint stringPoint9 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue13 = new C.NumericValue();
            numericValue13.Text = "In Progress";

            stringPoint9.Append(numericValue13);

            stringCache5.Append(pointCount7);
            stringCache5.Append(stringPoint9);

            stringReference5.Append(formula7);
            stringReference5.Append(stringCache5);

            seriesText3.Append(stringReference5);

            C.ChartShapeProperties chartShapeProperties13 = new C.ChartShapeProperties();

            A.SolidFill solidFill57 = new A.SolidFill();

            A.SchemeColor schemeColor92 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent2 };
            A.LuminanceModulation luminanceModulation46 = new A.LuminanceModulation() { Val = 60000 };
            A.LuminanceOffset luminanceOffset42 = new A.LuminanceOffset() { Val = 40000 };

            schemeColor92.Append(luminanceModulation46);
            schemeColor92.Append(luminanceOffset42);

            solidFill57.Append(schemeColor92);

            A.Outline outline48 = new A.Outline();
            A.NoFill noFill32 = new A.NoFill();

            outline48.Append(noFill32);
            A.EffectList effectList25 = new A.EffectList();
            A.Shape3DType shape3DType8 = new A.Shape3DType();

            chartShapeProperties13.Append(solidFill57);
            chartShapeProperties13.Append(outline48);
            chartShapeProperties13.Append(effectList25);
            chartShapeProperties13.Append(shape3DType8);
            C.InvertIfNegative invertIfNegative2 = new C.InvertIfNegative() { Val = false };

            C.DataLabels dataLabels3 = new C.DataLabels();

            C.ChartShapeProperties chartShapeProperties14 = new C.ChartShapeProperties();
            A.NoFill noFill33 = new A.NoFill();

            A.Outline outline49 = new A.Outline();
            A.NoFill noFill34 = new A.NoFill();

            outline49.Append(noFill34);
            A.EffectList effectList26 = new A.EffectList();

            chartShapeProperties14.Append(noFill33);
            chartShapeProperties14.Append(outline49);
            chartShapeProperties14.Append(effectList26);

            C.TextProperties textProperties9 = new C.TextProperties();

            A.BodyProperties bodyProperties12 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit7 = new A.ShapeAutoFit();

            bodyProperties12.Append(shapeAutoFit7);
            A.ListStyle listStyle12 = new A.ListStyle();

            A.Paragraph paragraph12 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties10 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties10 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill58 = new A.SolidFill();

            A.SchemeColor schemeColor93 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation47 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset43 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor93.Append(luminanceModulation47);
            schemeColor93.Append(luminanceOffset43);

            solidFill58.Append(schemeColor93);
            A.LatinFont latinFont9 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont9 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont9 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties10.Append(solidFill58);
            defaultRunProperties10.Append(latinFont9);
            defaultRunProperties10.Append(eastAsianFont9);
            defaultRunProperties10.Append(complexScriptFont9);

            paragraphProperties10.Append(defaultRunProperties10);
            A.EndParagraphRunProperties endParagraphRunProperties11 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph12.Append(paragraphProperties10);
            paragraph12.Append(endParagraphRunProperties11);

            textProperties9.Append(bodyProperties12);
            textProperties9.Append(listStyle12);
            textProperties9.Append(paragraph12);
            C.ShowLegendKey showLegendKey7 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue7 = new C.ShowValue() { Val = true };
            C.ShowCategoryName showCategoryName7 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName7 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent7 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize7 = new C.ShowBubbleSize() { Val = false };
            C.ShowLeaderLines showLeaderLines4 = new C.ShowLeaderLines() { Val = false };

            C.DLblsExtensionList dLblsExtensionList2 = new C.DLblsExtensionList();

            C.DLblsExtension dLblsExtension2 = new C.DLblsExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblsExtension2.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            C15.ShowLeaderLines showLeaderLines5 = new C15.ShowLeaderLines() { Val = true };

            C15.LeaderLines leaderLines2 = new C15.LeaderLines();

            C.ChartShapeProperties chartShapeProperties15 = new C.ChartShapeProperties();

            A.Outline outline50 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill59 = new A.SolidFill();

            A.SchemeColor schemeColor94 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation48 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset44 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor94.Append(luminanceModulation48);
            schemeColor94.Append(luminanceOffset44);

            solidFill59.Append(schemeColor94);
            A.Round round17 = new A.Round();

            outline50.Append(solidFill59);
            outline50.Append(round17);
            A.EffectList effectList27 = new A.EffectList();

            chartShapeProperties15.Append(outline50);
            chartShapeProperties15.Append(effectList27);

            leaderLines2.Append(chartShapeProperties15);

            dLblsExtension2.Append(showLeaderLines5);
            dLblsExtension2.Append(leaderLines2);

            dLblsExtensionList2.Append(dLblsExtension2);

            dataLabels3.Append(chartShapeProperties14);
            dataLabels3.Append(textProperties9);
            dataLabels3.Append(showLegendKey7);
            dataLabels3.Append(showValue7);
            dataLabels3.Append(showCategoryName7);
            dataLabels3.Append(showSeriesName7);
            dataLabels3.Append(showPercent7);
            dataLabels3.Append(showBubbleSize7);
            dataLabels3.Append(showLeaderLines4);
            dataLabels3.Append(dLblsExtensionList2);

            C.CategoryAxisData categoryAxisData3 = new C.CategoryAxisData();

            C.StringReference stringReference6 = new C.StringReference();
            C.Formula formula8 = new C.Formula();
            formula8.Text = "Cover!$A$35:$A$38";

            C.StringCache stringCache6 = new C.StringCache();
            C.PointCount pointCount8 = new C.PointCount() { Val = (UInt32Value)3U };

            C.StringPoint stringPoint10 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue14 = new C.NumericValue();
            numericValue14.Text = "ChemCo Distilling Visit";

            stringPoint10.Append(numericValue14);

            C.StringPoint stringPoint11 = new C.StringPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue15 = new C.NumericValue();
            numericValue15.Text = "Focus Bucket";

            stringPoint11.Append(numericValue15);

            C.StringPoint stringPoint12 = new C.StringPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue16 = new C.NumericValue();
            numericValue16.Text = "Redmond Site Visit";

            stringPoint12.Append(numericValue16);

            stringCache6.Append(pointCount8);
            stringCache6.Append(stringPoint10);
            stringCache6.Append(stringPoint11);
            stringCache6.Append(stringPoint12);

            stringReference6.Append(formula8);
            stringReference6.Append(stringCache6);

            categoryAxisData3.Append(stringReference6);

            C.Values values3 = new C.Values();

            C.NumberReference numberReference3 = new C.NumberReference();
            C.Formula formula9 = new C.Formula();
            formula9.Text = "Cover!$C$35:$C$38";

            C.NumberingCache numberingCache3 = new C.NumberingCache();
            C.FormatCode formatCode3 = new C.FormatCode();
            formatCode3.Text = "General";
            C.PointCount pointCount9 = new C.PointCount() { Val = (UInt32Value)3U };

            C.NumericPoint numericPoint5 = new C.NumericPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue17 = new C.NumericValue();
            numericValue17.Text = "2";

            numericPoint5.Append(numericValue17);

            C.NumericPoint numericPoint6 = new C.NumericPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue18 = new C.NumericValue();
            numericValue18.Text = "1";

            numericPoint6.Append(numericValue18);

            numberingCache3.Append(formatCode3);
            numberingCache3.Append(pointCount9);
            numberingCache3.Append(numericPoint5);
            numberingCache3.Append(numericPoint6);

            numberReference3.Append(formula9);
            numberReference3.Append(numberingCache3);

            values3.Append(numberReference3);

            C.BarSerExtensionList barSerExtensionList2 = new C.BarSerExtensionList();
            barSerExtensionList2.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            barSerExtensionList2.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            barSerExtensionList2.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            barSerExtensionList2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            barSerExtensionList2.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.BarSerExtension barSerExtension2 = new C.BarSerExtension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            barSerExtension2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement19 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000001-B1BC-48BA-8EDE-BB1B182C84D9}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            barSerExtension2.Append(openXmlUnknownElement19);

            barSerExtensionList2.Append(barSerExtension2);

            barChartSeries2.Append(index17);
            barChartSeries2.Append(order3);
            barChartSeries2.Append(seriesText3);
            barChartSeries2.Append(chartShapeProperties13);
            barChartSeries2.Append(invertIfNegative2);
            barChartSeries2.Append(dataLabels3);
            barChartSeries2.Append(categoryAxisData3);
            barChartSeries2.Append(values3);
            barChartSeries2.Append(barSerExtensionList2);

            C.BarChartSeries barChartSeries3 = new C.BarChartSeries();
            C.Index index18 = new C.Index() { Val = (UInt32Value)2U };
            C.Order order4 = new C.Order() { Val = (UInt32Value)2U };

            C.SeriesText seriesText4 = new C.SeriesText();

            C.StringReference stringReference7 = new C.StringReference();
            C.Formula formula10 = new C.Formula();
            formula10.Text = "Cover!$D$33:$D$34";

            C.StringCache stringCache7 = new C.StringCache();
            C.PointCount pointCount10 = new C.PointCount() { Val = (UInt32Value)1U };

            C.StringPoint stringPoint13 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue19 = new C.NumericValue();
            numericValue19.Text = "Not started";

            stringPoint13.Append(numericValue19);

            stringCache7.Append(pointCount10);
            stringCache7.Append(stringPoint13);

            stringReference7.Append(formula10);
            stringReference7.Append(stringCache7);

            seriesText4.Append(stringReference7);

            C.ChartShapeProperties chartShapeProperties16 = new C.ChartShapeProperties();

            A.SolidFill solidFill60 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex4 = new A.RgbColorModelHex() { Val = "FF4B4B" };

            solidFill60.Append(rgbColorModelHex4);

            A.Outline outline51 = new A.Outline();
            A.NoFill noFill35 = new A.NoFill();

            outline51.Append(noFill35);
            A.EffectList effectList28 = new A.EffectList();
            A.Shape3DType shape3DType9 = new A.Shape3DType();

            chartShapeProperties16.Append(solidFill60);
            chartShapeProperties16.Append(outline51);
            chartShapeProperties16.Append(effectList28);
            chartShapeProperties16.Append(shape3DType9);
            C.InvertIfNegative invertIfNegative3 = new C.InvertIfNegative() { Val = false };

            C.DataLabels dataLabels4 = new C.DataLabels();

            C.ChartShapeProperties chartShapeProperties17 = new C.ChartShapeProperties();
            A.NoFill noFill36 = new A.NoFill();

            A.Outline outline52 = new A.Outline();
            A.NoFill noFill37 = new A.NoFill();

            outline52.Append(noFill37);
            A.EffectList effectList29 = new A.EffectList();

            chartShapeProperties17.Append(noFill36);
            chartShapeProperties17.Append(outline52);
            chartShapeProperties17.Append(effectList29);

            C.TextProperties textProperties10 = new C.TextProperties();

            A.BodyProperties bodyProperties13 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 38100, TopInset = 19050, RightInset = 38100, BottomInset = 19050, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit8 = new A.ShapeAutoFit();

            bodyProperties13.Append(shapeAutoFit8);
            A.ListStyle listStyle13 = new A.ListStyle();

            A.Paragraph paragraph13 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties11 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties11 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill61 = new A.SolidFill();

            A.SchemeColor schemeColor95 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation49 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset45 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor95.Append(luminanceModulation49);
            schemeColor95.Append(luminanceOffset45);

            solidFill61.Append(schemeColor95);
            A.LatinFont latinFont10 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont10 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont10 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties11.Append(solidFill61);
            defaultRunProperties11.Append(latinFont10);
            defaultRunProperties11.Append(eastAsianFont10);
            defaultRunProperties11.Append(complexScriptFont10);

            paragraphProperties11.Append(defaultRunProperties11);
            A.EndParagraphRunProperties endParagraphRunProperties12 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph13.Append(paragraphProperties11);
            paragraph13.Append(endParagraphRunProperties12);

            textProperties10.Append(bodyProperties13);
            textProperties10.Append(listStyle13);
            textProperties10.Append(paragraph13);
            C.ShowLegendKey showLegendKey8 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue8 = new C.ShowValue() { Val = true };
            C.ShowCategoryName showCategoryName8 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName8 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent8 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize8 = new C.ShowBubbleSize() { Val = false };
            C.ShowLeaderLines showLeaderLines6 = new C.ShowLeaderLines() { Val = false };

            C.DLblsExtensionList dLblsExtensionList3 = new C.DLblsExtensionList();

            C.DLblsExtension dLblsExtension3 = new C.DLblsExtension() { Uri = "{CE6537A1-D6FC-4f65-9D91-7224C49458BB}" };
            dLblsExtension3.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            C15.ShowLeaderLines showLeaderLines7 = new C15.ShowLeaderLines() { Val = true };

            C15.LeaderLines leaderLines3 = new C15.LeaderLines();

            C.ChartShapeProperties chartShapeProperties18 = new C.ChartShapeProperties();

            A.Outline outline53 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill62 = new A.SolidFill();

            A.SchemeColor schemeColor96 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation50 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset46 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor96.Append(luminanceModulation50);
            schemeColor96.Append(luminanceOffset46);

            solidFill62.Append(schemeColor96);
            A.Round round18 = new A.Round();

            outline53.Append(solidFill62);
            outline53.Append(round18);
            A.EffectList effectList30 = new A.EffectList();

            chartShapeProperties18.Append(outline53);
            chartShapeProperties18.Append(effectList30);

            leaderLines3.Append(chartShapeProperties18);

            dLblsExtension3.Append(showLeaderLines7);
            dLblsExtension3.Append(leaderLines3);

            dLblsExtensionList3.Append(dLblsExtension3);

            dataLabels4.Append(chartShapeProperties17);
            dataLabels4.Append(textProperties10);
            dataLabels4.Append(showLegendKey8);
            dataLabels4.Append(showValue8);
            dataLabels4.Append(showCategoryName8);
            dataLabels4.Append(showSeriesName8);
            dataLabels4.Append(showPercent8);
            dataLabels4.Append(showBubbleSize8);
            dataLabels4.Append(showLeaderLines6);
            dataLabels4.Append(dLblsExtensionList3);

            C.CategoryAxisData categoryAxisData4 = new C.CategoryAxisData();

            C.StringReference stringReference8 = new C.StringReference();
            C.Formula formula11 = new C.Formula();
            formula11.Text = "Cover!$A$35:$A$38";

            C.StringCache stringCache8 = new C.StringCache();
            C.PointCount pointCount11 = new C.PointCount() { Val = (UInt32Value)3U };

            C.StringPoint stringPoint14 = new C.StringPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue20 = new C.NumericValue();
            numericValue20.Text = "ChemCo Distilling Visit";

            stringPoint14.Append(numericValue20);

            C.StringPoint stringPoint15 = new C.StringPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue21 = new C.NumericValue();
            numericValue21.Text = "Focus Bucket";

            stringPoint15.Append(numericValue21);

            C.StringPoint stringPoint16 = new C.StringPoint() { Index = (UInt32Value)2U };
            C.NumericValue numericValue22 = new C.NumericValue();
            numericValue22.Text = "Redmond Site Visit";

            stringPoint16.Append(numericValue22);

            stringCache8.Append(pointCount11);
            stringCache8.Append(stringPoint14);
            stringCache8.Append(stringPoint15);
            stringCache8.Append(stringPoint16);

            stringReference8.Append(formula11);
            stringReference8.Append(stringCache8);

            categoryAxisData4.Append(stringReference8);

            C.Values values4 = new C.Values();

            C.NumberReference numberReference4 = new C.NumberReference();
            C.Formula formula12 = new C.Formula();
            formula12.Text = "Cover!$D$35:$D$38";

            C.NumberingCache numberingCache4 = new C.NumberingCache();
            C.FormatCode formatCode4 = new C.FormatCode();
            formatCode4.Text = "General";
            C.PointCount pointCount12 = new C.PointCount() { Val = (UInt32Value)3U };

            C.NumericPoint numericPoint7 = new C.NumericPoint() { Index = (UInt32Value)0U };
            C.NumericValue numericValue23 = new C.NumericValue();
            numericValue23.Text = "1";

            numericPoint7.Append(numericValue23);

            C.NumericPoint numericPoint8 = new C.NumericPoint() { Index = (UInt32Value)1U };
            C.NumericValue numericValue24 = new C.NumericValue();
            numericValue24.Text = "2";

            numericPoint8.Append(numericValue24);

            numberingCache4.Append(formatCode4);
            numberingCache4.Append(pointCount12);
            numberingCache4.Append(numericPoint7);
            numberingCache4.Append(numericPoint8);

            numberReference4.Append(formula12);
            numberReference4.Append(numberingCache4);

            values4.Append(numberReference4);

            C.BarSerExtensionList barSerExtensionList3 = new C.BarSerExtensionList();
            barSerExtensionList3.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            barSerExtensionList3.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            barSerExtensionList3.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            barSerExtensionList3.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            barSerExtensionList3.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.BarSerExtension barSerExtension3 = new C.BarSerExtension() { Uri = "{C3380CC4-5D6E-409C-BE32-E72D297353CC}" };
            barSerExtension3.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement20 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:uniqueId val=\"{00000002-B1BC-48BA-8EDE-BB1B182C84D9}\" xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\" />");

            barSerExtension3.Append(openXmlUnknownElement20);

            barSerExtensionList3.Append(barSerExtension3);

            barChartSeries3.Append(index18);
            barChartSeries3.Append(order4);
            barChartSeries3.Append(seriesText4);
            barChartSeries3.Append(chartShapeProperties16);
            barChartSeries3.Append(invertIfNegative3);
            barChartSeries3.Append(dataLabels4);
            barChartSeries3.Append(categoryAxisData4);
            barChartSeries3.Append(values4);
            barChartSeries3.Append(barSerExtensionList3);

            C.DataLabels dataLabels5 = new C.DataLabels();
            C.ShowLegendKey showLegendKey9 = new C.ShowLegendKey() { Val = false };
            C.ShowValue showValue9 = new C.ShowValue() { Val = false };
            C.ShowCategoryName showCategoryName9 = new C.ShowCategoryName() { Val = false };
            C.ShowSeriesName showSeriesName9 = new C.ShowSeriesName() { Val = false };
            C.ShowPercent showPercent9 = new C.ShowPercent() { Val = false };
            C.ShowBubbleSize showBubbleSize9 = new C.ShowBubbleSize() { Val = false };

            dataLabels5.Append(showLegendKey9);
            dataLabels5.Append(showValue9);
            dataLabels5.Append(showCategoryName9);
            dataLabels5.Append(showSeriesName9);
            dataLabels5.Append(showPercent9);
            dataLabels5.Append(showBubbleSize9);
            C.GapWidth gapWidth1 = new C.GapWidth() { Val = (UInt16Value)150U };
            C.Shape shape3 = new C.Shape() { Val = C.ShapeValues.Box };
            C.AxisId axisId1 = new C.AxisId() { Val = (UInt32Value)1278644320U };
            C.AxisId axisId2 = new C.AxisId() { Val = (UInt32Value)1301850288U };
            C.AxisId axisId3 = new C.AxisId() { Val = (UInt32Value)0U };

            bar3DChart1.Append(barDirection1);
            bar3DChart1.Append(barGrouping1);
            bar3DChart1.Append(varyColors2);
            bar3DChart1.Append(barChartSeries1);
            bar3DChart1.Append(barChartSeries2);
            bar3DChart1.Append(barChartSeries3);
            bar3DChart1.Append(dataLabels5);
            bar3DChart1.Append(gapWidth1);
            bar3DChart1.Append(shape3);
            bar3DChart1.Append(axisId1);
            bar3DChart1.Append(axisId2);
            bar3DChart1.Append(axisId3);

            C.CategoryAxis categoryAxis2 = new C.CategoryAxis();
            C.AxisId axisId4 = new C.AxisId() { Val = (UInt32Value)1278644320U };

            C.Scaling scaling1 = new C.Scaling();
            C.Orientation orientation1 = new C.Orientation() { Val = C.OrientationValues.MinMax };

            scaling1.Append(orientation1);
            C.Delete delete1 = new C.Delete() { Val = false };
            C.AxisPosition axisPosition1 = new C.AxisPosition() { Val = C.AxisPositionValues.Bottom };
            C.NumberingFormat numberingFormat1 = new C.NumberingFormat() { FormatCode = "General", SourceLinked = true };
            C.MajorTickMark majorTickMark1 = new C.MajorTickMark() { Val = C.TickMarkValues.None };
            C.MinorTickMark minorTickMark1 = new C.MinorTickMark() { Val = C.TickMarkValues.None };
            C.TickLabelPosition tickLabelPosition1 = new C.TickLabelPosition() { Val = C.TickLabelPositionValues.NextTo };

            C.ChartShapeProperties chartShapeProperties19 = new C.ChartShapeProperties();
            A.NoFill noFill38 = new A.NoFill();

            A.Outline outline54 = new A.Outline();
            A.NoFill noFill39 = new A.NoFill();

            outline54.Append(noFill39);
            A.EffectList effectList31 = new A.EffectList();

            chartShapeProperties19.Append(noFill38);
            chartShapeProperties19.Append(outline54);
            chartShapeProperties19.Append(effectList31);

            C.TextProperties textProperties11 = new C.TextProperties();
            A.BodyProperties bodyProperties14 = new A.BodyProperties() { Rotation = -60000000, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ListStyle listStyle14 = new A.ListStyle();

            A.Paragraph paragraph14 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties12 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties12 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill63 = new A.SolidFill();

            A.SchemeColor schemeColor97 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation51 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset47 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor97.Append(luminanceModulation51);
            schemeColor97.Append(luminanceOffset47);

            solidFill63.Append(schemeColor97);
            A.LatinFont latinFont11 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont11 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont11 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties12.Append(solidFill63);
            defaultRunProperties12.Append(latinFont11);
            defaultRunProperties12.Append(eastAsianFont11);
            defaultRunProperties12.Append(complexScriptFont11);

            paragraphProperties12.Append(defaultRunProperties12);
            A.EndParagraphRunProperties endParagraphRunProperties13 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph14.Append(paragraphProperties12);
            paragraph14.Append(endParagraphRunProperties13);

            textProperties11.Append(bodyProperties14);
            textProperties11.Append(listStyle14);
            textProperties11.Append(paragraph14);
            C.CrossingAxis crossingAxis1 = new C.CrossingAxis() { Val = (UInt32Value)1301850288U };
            C.Crosses crosses1 = new C.Crosses() { Val = C.CrossesValues.AutoZero };
            C.AutoLabeled autoLabeled1 = new C.AutoLabeled() { Val = true };
            C.LabelAlignment labelAlignment1 = new C.LabelAlignment() { Val = C.LabelAlignmentValues.Center };
            C.LabelOffset labelOffset1 = new C.LabelOffset() { Val = (UInt16Value)100U };
            C.NoMultiLevelLabels noMultiLevelLabels1 = new C.NoMultiLevelLabels() { Val = false };

            categoryAxis2.Append(axisId4);
            categoryAxis2.Append(scaling1);
            categoryAxis2.Append(delete1);
            categoryAxis2.Append(axisPosition1);
            categoryAxis2.Append(numberingFormat1);
            categoryAxis2.Append(majorTickMark1);
            categoryAxis2.Append(minorTickMark1);
            categoryAxis2.Append(tickLabelPosition1);
            categoryAxis2.Append(chartShapeProperties19);
            categoryAxis2.Append(textProperties11);
            categoryAxis2.Append(crossingAxis1);
            categoryAxis2.Append(crosses1);
            categoryAxis2.Append(autoLabeled1);
            categoryAxis2.Append(labelAlignment1);
            categoryAxis2.Append(labelOffset1);
            categoryAxis2.Append(noMultiLevelLabels1);

            C.ValueAxis valueAxis2 = new C.ValueAxis();
            C.AxisId axisId5 = new C.AxisId() { Val = (UInt32Value)1301850288U };

            C.Scaling scaling2 = new C.Scaling();
            C.Orientation orientation2 = new C.Orientation() { Val = C.OrientationValues.MinMax };

            scaling2.Append(orientation2);
            C.Delete delete2 = new C.Delete() { Val = false };
            C.AxisPosition axisPosition2 = new C.AxisPosition() { Val = C.AxisPositionValues.Left };

            C.MajorGridlines majorGridlines1 = new C.MajorGridlines();

            C.ChartShapeProperties chartShapeProperties20 = new C.ChartShapeProperties();

            A.Outline outline55 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };
            A.NoFill noFill40 = new A.NoFill();
            A.Round round19 = new A.Round();

            outline55.Append(noFill40);
            outline55.Append(round19);
            A.EffectList effectList32 = new A.EffectList();

            chartShapeProperties20.Append(outline55);
            chartShapeProperties20.Append(effectList32);

            majorGridlines1.Append(chartShapeProperties20);
            C.NumberingFormat numberingFormat2 = new C.NumberingFormat() { FormatCode = "General", SourceLinked = true };
            C.MajorTickMark majorTickMark2 = new C.MajorTickMark() { Val = C.TickMarkValues.None };
            C.MinorTickMark minorTickMark2 = new C.MinorTickMark() { Val = C.TickMarkValues.None };
            C.TickLabelPosition tickLabelPosition2 = new C.TickLabelPosition() { Val = C.TickLabelPositionValues.None };

            C.ChartShapeProperties chartShapeProperties21 = new C.ChartShapeProperties();
            A.NoFill noFill41 = new A.NoFill();

            A.Outline outline56 = new A.Outline();
            A.NoFill noFill42 = new A.NoFill();

            outline56.Append(noFill42);
            A.EffectList effectList33 = new A.EffectList();

            chartShapeProperties21.Append(noFill41);
            chartShapeProperties21.Append(outline56);
            chartShapeProperties21.Append(effectList33);

            C.TextProperties textProperties12 = new C.TextProperties();
            A.BodyProperties bodyProperties15 = new A.BodyProperties() { Rotation = -60000000, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ListStyle listStyle15 = new A.ListStyle();

            A.Paragraph paragraph15 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties13 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties13 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill64 = new A.SolidFill();

            A.SchemeColor schemeColor98 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation52 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset48 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor98.Append(luminanceModulation52);
            schemeColor98.Append(luminanceOffset48);

            solidFill64.Append(schemeColor98);
            A.LatinFont latinFont12 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont12 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont12 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties13.Append(solidFill64);
            defaultRunProperties13.Append(latinFont12);
            defaultRunProperties13.Append(eastAsianFont12);
            defaultRunProperties13.Append(complexScriptFont12);

            paragraphProperties13.Append(defaultRunProperties13);
            A.EndParagraphRunProperties endParagraphRunProperties14 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph15.Append(paragraphProperties13);
            paragraph15.Append(endParagraphRunProperties14);

            textProperties12.Append(bodyProperties15);
            textProperties12.Append(listStyle15);
            textProperties12.Append(paragraph15);
            C.CrossingAxis crossingAxis2 = new C.CrossingAxis() { Val = (UInt32Value)1278644320U };
            C.Crosses crosses2 = new C.Crosses() { Val = C.CrossesValues.AutoZero };
            C.CrossBetween crossBetween1 = new C.CrossBetween() { Val = C.CrossBetweenValues.Between };

            valueAxis2.Append(axisId5);
            valueAxis2.Append(scaling2);
            valueAxis2.Append(delete2);
            valueAxis2.Append(axisPosition2);
            valueAxis2.Append(majorGridlines1);
            valueAxis2.Append(numberingFormat2);
            valueAxis2.Append(majorTickMark2);
            valueAxis2.Append(minorTickMark2);
            valueAxis2.Append(tickLabelPosition2);
            valueAxis2.Append(chartShapeProperties21);
            valueAxis2.Append(textProperties12);
            valueAxis2.Append(crossingAxis2);
            valueAxis2.Append(crosses2);
            valueAxis2.Append(crossBetween1);

            C.ShapeProperties shapeProperties38 = new C.ShapeProperties();
            A.NoFill noFill43 = new A.NoFill();

            A.Outline outline57 = new A.Outline();
            A.NoFill noFill44 = new A.NoFill();

            outline57.Append(noFill44);
            A.EffectList effectList34 = new A.EffectList();

            shapeProperties38.Append(noFill43);
            shapeProperties38.Append(outline57);
            shapeProperties38.Append(effectList34);

            plotArea3.Append(layout2);
            plotArea3.Append(bar3DChart1);
            plotArea3.Append(categoryAxis2);
            plotArea3.Append(valueAxis2);
            plotArea3.Append(shapeProperties38);

            C.Legend legend2 = new C.Legend();
            C.LegendPosition legendPosition2 = new C.LegendPosition() { Val = C.LegendPositionValues.Right };
            C.Overlay overlay3 = new C.Overlay() { Val = false };

            C.ChartShapeProperties chartShapeProperties22 = new C.ChartShapeProperties();
            A.NoFill noFill45 = new A.NoFill();

            A.Outline outline58 = new A.Outline();
            A.NoFill noFill46 = new A.NoFill();

            outline58.Append(noFill46);
            A.EffectList effectList35 = new A.EffectList();

            chartShapeProperties22.Append(noFill45);
            chartShapeProperties22.Append(outline58);
            chartShapeProperties22.Append(effectList35);

            C.TextProperties textProperties13 = new C.TextProperties();
            A.BodyProperties bodyProperties16 = new A.BodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ListStyle listStyle16 = new A.ListStyle();

            A.Paragraph paragraph16 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties14 = new A.ParagraphProperties();

            A.DefaultRunProperties defaultRunProperties14 = new A.DefaultRunProperties() { FontSize = 900, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Kerning = 1200, Baseline = 0 };

            A.SolidFill solidFill65 = new A.SolidFill();

            A.SchemeColor schemeColor99 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation53 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset49 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor99.Append(luminanceModulation53);
            schemeColor99.Append(luminanceOffset49);

            solidFill65.Append(schemeColor99);
            A.LatinFont latinFont13 = new A.LatinFont() { Typeface = "+mn-lt" };
            A.EastAsianFont eastAsianFont13 = new A.EastAsianFont() { Typeface = "+mn-ea" };
            A.ComplexScriptFont complexScriptFont13 = new A.ComplexScriptFont() { Typeface = "+mn-cs" };

            defaultRunProperties14.Append(solidFill65);
            defaultRunProperties14.Append(latinFont13);
            defaultRunProperties14.Append(eastAsianFont13);
            defaultRunProperties14.Append(complexScriptFont13);

            paragraphProperties14.Append(defaultRunProperties14);
            A.EndParagraphRunProperties endParagraphRunProperties15 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph16.Append(paragraphProperties14);
            paragraph16.Append(endParagraphRunProperties15);

            textProperties13.Append(bodyProperties16);
            textProperties13.Append(listStyle16);
            textProperties13.Append(paragraph16);

            legend2.Append(legendPosition2);
            legend2.Append(overlay3);
            legend2.Append(chartShapeProperties22);
            legend2.Append(textProperties13);
            C.PlotVisibleOnly plotVisibleOnly2 = new C.PlotVisibleOnly() { Val = true };
            C.DisplayBlanksAs displayBlanksAs2 = new C.DisplayBlanksAs() { Val = C.DisplayBlanksAsValues.Gap };

            C.ExtensionList extensionList5 = new C.ExtensionList();
            extensionList5.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            extensionList5.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            extensionList5.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            extensionList5.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            extensionList5.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.Extension extension6 = new C.Extension() { Uri = "{56B9EC1D-385E-4148-901F-78D8002777C0}" };
            extension6.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            OpenXmlUnknownElement openXmlUnknownElement21 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16r3:dataDisplayOptions16 xmlns:c16r3=\"http://schemas.microsoft.com/office/drawing/2017/03/chart\"><c16r3:dispNaAsBlank val=\"1\" /></c16r3:dataDisplayOptions16>");

            extension6.Append(openXmlUnknownElement21);

            extensionList5.Append(extension6);
            C.ShowDataLabelsOverMaximum showDataLabelsOverMaximum2 = new C.ShowDataLabelsOverMaximum() { Val = false };

            chart2.Append(autoTitleDeleted2);
            chart2.Append(pivotFormats2);
            chart2.Append(view3D1);
            chart2.Append(floor2);
            chart2.Append(sideWall1);
            chart2.Append(backWall1);
            chart2.Append(plotArea3);
            chart2.Append(legend2);
            chart2.Append(plotVisibleOnly2);
            chart2.Append(displayBlanksAs2);
            chart2.Append(extensionList5);
            chart2.Append(showDataLabelsOverMaximum2);

            C.ShapeProperties shapeProperties39 = new C.ShapeProperties();

            A.SolidFill solidFill66 = new A.SolidFill();
            A.SchemeColor schemeColor100 = new A.SchemeColor() { Val = A.SchemeColorValues.Background1 };

            solidFill66.Append(schemeColor100);

            A.Outline outline59 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill67 = new A.SolidFill();

            A.SchemeColor schemeColor101 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation54 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset50 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor101.Append(luminanceModulation54);
            schemeColor101.Append(luminanceOffset50);

            solidFill67.Append(schemeColor101);
            A.Round round20 = new A.Round();

            outline59.Append(solidFill67);
            outline59.Append(round20);
            A.EffectList effectList36 = new A.EffectList();

            shapeProperties39.Append(solidFill66);
            shapeProperties39.Append(outline59);
            shapeProperties39.Append(effectList36);

            C.TextProperties textProperties14 = new C.TextProperties();
            A.BodyProperties bodyProperties17 = new A.BodyProperties();
            A.ListStyle listStyle17 = new A.ListStyle();

            A.Paragraph paragraph17 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties15 = new A.ParagraphProperties();
            A.DefaultRunProperties defaultRunProperties15 = new A.DefaultRunProperties();

            paragraphProperties15.Append(defaultRunProperties15);
            A.EndParagraphRunProperties endParagraphRunProperties16 = new A.EndParagraphRunProperties() { Language = "en-US" };

            paragraph17.Append(paragraphProperties15);
            paragraph17.Append(endParagraphRunProperties16);

            textProperties14.Append(bodyProperties17);
            textProperties14.Append(listStyle17);
            textProperties14.Append(paragraph17);

            C.PrintSettings printSettings2 = new C.PrintSettings();
            C.HeaderFooter headerFooter2 = new C.HeaderFooter();
            C.PageMargins pageMargins3 = new C.PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            C.PageSetup pageSetup3 = new C.PageSetup();

            printSettings2.Append(headerFooter2);
            printSettings2.Append(pageMargins3);
            printSettings2.Append(pageSetup3);

            C.ChartSpaceExtensionList chartSpaceExtensionList2 = new C.ChartSpaceExtensionList();
            chartSpaceExtensionList2.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            chartSpaceExtensionList2.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");
            chartSpaceExtensionList2.AddNamespaceDeclaration("c15", "http://schemas.microsoft.com/office/drawing/2012/chart");
            chartSpaceExtensionList2.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");
            chartSpaceExtensionList2.AddNamespaceDeclaration("c16r3", "http://schemas.microsoft.com/office/drawing/2017/03/chart");

            C.ChartSpaceExtension chartSpaceExtension3 = new C.ChartSpaceExtension() { Uri = "{781A3756-C4B2-4CAC-9D66-4F8BD8637D16}" };
            chartSpaceExtension3.AddNamespaceDeclaration("c14", "http://schemas.microsoft.com/office/drawing/2007/8/2/chart");

            C14.PivotOptions pivotOptions2 = new C14.PivotOptions();
            C14.DropZoneFilter dropZoneFilter2 = new C14.DropZoneFilter() { Val = true };
            C14.DropZoneCategories dropZoneCategories2 = new C14.DropZoneCategories() { Val = true };
            C14.DropZoneData dropZoneData2 = new C14.DropZoneData() { Val = true };
            C14.DropZoneSeries dropZoneSeries2 = new C14.DropZoneSeries() { Val = true };
            C14.DropZonesVisible dropZonesVisible2 = new C14.DropZonesVisible() { Val = true };

            pivotOptions2.Append(dropZoneFilter2);
            pivotOptions2.Append(dropZoneCategories2);
            pivotOptions2.Append(dropZoneData2);
            pivotOptions2.Append(dropZoneSeries2);
            pivotOptions2.Append(dropZonesVisible2);

            chartSpaceExtension3.Append(pivotOptions2);

            C.ChartSpaceExtension chartSpaceExtension4 = new C.ChartSpaceExtension() { Uri = "{E28EC0CA-F0BB-4C9C-879D-F8772B89E7AC}" };
            chartSpaceExtension4.AddNamespaceDeclaration("c16", "http://schemas.microsoft.com/office/drawing/2014/chart");

            OpenXmlUnknownElement openXmlUnknownElement22 = OpenXmlUnknownElement.CreateOpenXmlUnknownElement("<c16:pivotOptions16 xmlns:c16=\"http://schemas.microsoft.com/office/drawing/2014/chart\"><c16:showExpandCollapseFieldButtons val=\"1\" /></c16:pivotOptions16>");

            chartSpaceExtension4.Append(openXmlUnknownElement22);

            chartSpaceExtensionList2.Append(chartSpaceExtension3);
            chartSpaceExtensionList2.Append(chartSpaceExtension4);

            chartSpace2.Append(date19042);
            chartSpace2.Append(editingLanguage2);
            chartSpace2.Append(roundedCorners2);
            chartSpace2.Append(alternateContent3);
            chartSpace2.Append(pivotSource2);
            chartSpace2.Append(chart2);
            chartSpace2.Append(shapeProperties39);
            chartSpace2.Append(textProperties14);
            chartSpace2.Append(printSettings2);
            chartSpace2.Append(chartSpaceExtensionList2);

            chartPart2.ChartSpace = chartSpace2;
        }

        // Generates content of chartColorStylePart2.
        private void GenerateChartColorStylePart2Content(ChartColorStylePart chartColorStylePart2)
        {
            Cs.ColorStyle colorStyle2 = new Cs.ColorStyle() { Method = "cycle", Id = (UInt32Value)10U };
            colorStyle2.AddNamespaceDeclaration("cs", "http://schemas.microsoft.com/office/drawing/2012/chartStyle");
            colorStyle2.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            A.SchemeColor schemeColor102 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent1 };
            A.SchemeColor schemeColor103 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent2 };
            A.SchemeColor schemeColor104 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent3 };
            A.SchemeColor schemeColor105 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent4 };
            A.SchemeColor schemeColor106 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent5 };
            A.SchemeColor schemeColor107 = new A.SchemeColor() { Val = A.SchemeColorValues.Accent6 };
            Cs.ColorStyleVariation colorStyleVariation10 = new Cs.ColorStyleVariation();

            Cs.ColorStyleVariation colorStyleVariation11 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation55 = new A.LuminanceModulation() { Val = 60000 };

            colorStyleVariation11.Append(luminanceModulation55);

            Cs.ColorStyleVariation colorStyleVariation12 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation56 = new A.LuminanceModulation() { Val = 80000 };
            A.LuminanceOffset luminanceOffset51 = new A.LuminanceOffset() { Val = 20000 };

            colorStyleVariation12.Append(luminanceModulation56);
            colorStyleVariation12.Append(luminanceOffset51);

            Cs.ColorStyleVariation colorStyleVariation13 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation57 = new A.LuminanceModulation() { Val = 80000 };

            colorStyleVariation13.Append(luminanceModulation57);

            Cs.ColorStyleVariation colorStyleVariation14 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation58 = new A.LuminanceModulation() { Val = 60000 };
            A.LuminanceOffset luminanceOffset52 = new A.LuminanceOffset() { Val = 40000 };

            colorStyleVariation14.Append(luminanceModulation58);
            colorStyleVariation14.Append(luminanceOffset52);

            Cs.ColorStyleVariation colorStyleVariation15 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation59 = new A.LuminanceModulation() { Val = 50000 };

            colorStyleVariation15.Append(luminanceModulation59);

            Cs.ColorStyleVariation colorStyleVariation16 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation60 = new A.LuminanceModulation() { Val = 70000 };
            A.LuminanceOffset luminanceOffset53 = new A.LuminanceOffset() { Val = 30000 };

            colorStyleVariation16.Append(luminanceModulation60);
            colorStyleVariation16.Append(luminanceOffset53);

            Cs.ColorStyleVariation colorStyleVariation17 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation61 = new A.LuminanceModulation() { Val = 70000 };

            colorStyleVariation17.Append(luminanceModulation61);

            Cs.ColorStyleVariation colorStyleVariation18 = new Cs.ColorStyleVariation();
            A.LuminanceModulation luminanceModulation62 = new A.LuminanceModulation() { Val = 50000 };
            A.LuminanceOffset luminanceOffset54 = new A.LuminanceOffset() { Val = 50000 };

            colorStyleVariation18.Append(luminanceModulation62);
            colorStyleVariation18.Append(luminanceOffset54);

            colorStyle2.Append(schemeColor102);
            colorStyle2.Append(schemeColor103);
            colorStyle2.Append(schemeColor104);
            colorStyle2.Append(schemeColor105);
            colorStyle2.Append(schemeColor106);
            colorStyle2.Append(schemeColor107);
            colorStyle2.Append(colorStyleVariation10);
            colorStyle2.Append(colorStyleVariation11);
            colorStyle2.Append(colorStyleVariation12);
            colorStyle2.Append(colorStyleVariation13);
            colorStyle2.Append(colorStyleVariation14);
            colorStyle2.Append(colorStyleVariation15);
            colorStyle2.Append(colorStyleVariation16);
            colorStyle2.Append(colorStyleVariation17);
            colorStyle2.Append(colorStyleVariation18);

            chartColorStylePart2.ColorStyle = colorStyle2;
        }

        // Generates content of chartStylePart2.
        private void GenerateChartStylePart2Content(ChartStylePart chartStylePart2)
        {
            Cs.ChartStyle chartStyle2 = new Cs.ChartStyle() { Id = (UInt32Value)286U };
            chartStyle2.AddNamespaceDeclaration("cs", "http://schemas.microsoft.com/office/drawing/2012/chartStyle");
            chartStyle2.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            Cs.AxisTitle axisTitle2 = new Cs.AxisTitle();
            Cs.LineReference lineReference33 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference33 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference33 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference33 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor108 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation63 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset55 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor108.Append(luminanceModulation63);
            schemeColor108.Append(luminanceOffset55);

            fontReference33.Append(schemeColor108);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType12 = new Cs.TextCharacterPropertiesType() { FontSize = 1000, Kerning = 1200 };

            axisTitle2.Append(lineReference33);
            axisTitle2.Append(fillReference33);
            axisTitle2.Append(effectReference33);
            axisTitle2.Append(fontReference33);
            axisTitle2.Append(textCharacterPropertiesType12);

            Cs.CategoryAxis categoryAxis3 = new Cs.CategoryAxis();
            Cs.LineReference lineReference34 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference34 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference34 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference34 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor109 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation64 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset56 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor109.Append(luminanceModulation64);
            schemeColor109.Append(luminanceOffset56);

            fontReference34.Append(schemeColor109);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType13 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            categoryAxis3.Append(lineReference34);
            categoryAxis3.Append(fillReference34);
            categoryAxis3.Append(effectReference34);
            categoryAxis3.Append(fontReference34);
            categoryAxis3.Append(textCharacterPropertiesType13);

            Cs.ChartArea chartArea2 = new Cs.ChartArea() { Modifiers = new ListValue<StringValue>() { InnerText = "allowNoFillOverride allowNoLineOverride" } };
            Cs.LineReference lineReference35 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference35 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference35 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference35 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor110 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference35.Append(schemeColor110);

            Cs.ShapeProperties shapeProperties40 = new Cs.ShapeProperties();

            A.SolidFill solidFill68 = new A.SolidFill();
            A.SchemeColor schemeColor111 = new A.SchemeColor() { Val = A.SchemeColorValues.Background1 };

            solidFill68.Append(schemeColor111);

            A.Outline outline60 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill69 = new A.SolidFill();

            A.SchemeColor schemeColor112 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation65 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset57 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor112.Append(luminanceModulation65);
            schemeColor112.Append(luminanceOffset57);

            solidFill69.Append(schemeColor112);
            A.Round round21 = new A.Round();

            outline60.Append(solidFill69);
            outline60.Append(round21);

            shapeProperties40.Append(solidFill68);
            shapeProperties40.Append(outline60);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType14 = new Cs.TextCharacterPropertiesType() { FontSize = 1000, Kerning = 1200 };

            chartArea2.Append(lineReference35);
            chartArea2.Append(fillReference35);
            chartArea2.Append(effectReference35);
            chartArea2.Append(fontReference35);
            chartArea2.Append(shapeProperties40);
            chartArea2.Append(textCharacterPropertiesType14);

            Cs.DataLabel dataLabel6 = new Cs.DataLabel();
            Cs.LineReference lineReference36 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference36 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference36 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference36 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor113 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation66 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset58 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor113.Append(luminanceModulation66);
            schemeColor113.Append(luminanceOffset58);

            fontReference36.Append(schemeColor113);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType15 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            dataLabel6.Append(lineReference36);
            dataLabel6.Append(fillReference36);
            dataLabel6.Append(effectReference36);
            dataLabel6.Append(fontReference36);
            dataLabel6.Append(textCharacterPropertiesType15);

            Cs.DataLabelCallout dataLabelCallout2 = new Cs.DataLabelCallout();
            Cs.LineReference lineReference37 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference37 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference37 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference37 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor114 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };
            A.LuminanceModulation luminanceModulation67 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset59 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor114.Append(luminanceModulation67);
            schemeColor114.Append(luminanceOffset59);

            fontReference37.Append(schemeColor114);

            Cs.ShapeProperties shapeProperties41 = new Cs.ShapeProperties();

            A.SolidFill solidFill70 = new A.SolidFill();
            A.SchemeColor schemeColor115 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill70.Append(schemeColor115);

            A.Outline outline61 = new A.Outline();

            A.SolidFill solidFill71 = new A.SolidFill();

            A.SchemeColor schemeColor116 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };
            A.LuminanceModulation luminanceModulation68 = new A.LuminanceModulation() { Val = 25000 };
            A.LuminanceOffset luminanceOffset60 = new A.LuminanceOffset() { Val = 75000 };

            schemeColor116.Append(luminanceModulation68);
            schemeColor116.Append(luminanceOffset60);

            solidFill71.Append(schemeColor116);

            outline61.Append(solidFill71);

            shapeProperties41.Append(solidFill70);
            shapeProperties41.Append(outline61);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType16 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            Cs.TextBodyProperties textBodyProperties2 = new Cs.TextBodyProperties() { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Clip, HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, Vertical = A.TextVerticalValues.Horizontal, Wrap = A.TextWrappingValues.Square, LeftInset = 36576, TopInset = 18288, RightInset = 36576, BottomInset = 18288, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true };
            A.ShapeAutoFit shapeAutoFit9 = new A.ShapeAutoFit();

            textBodyProperties2.Append(shapeAutoFit9);

            dataLabelCallout2.Append(lineReference37);
            dataLabelCallout2.Append(fillReference37);
            dataLabelCallout2.Append(effectReference37);
            dataLabelCallout2.Append(fontReference37);
            dataLabelCallout2.Append(shapeProperties41);
            dataLabelCallout2.Append(textCharacterPropertiesType16);
            dataLabelCallout2.Append(textBodyProperties2);

            Cs.DataPoint dataPoint5 = new Cs.DataPoint();
            Cs.LineReference lineReference38 = new Cs.LineReference() { Index = (UInt32Value)0U };

            Cs.FillReference fillReference38 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.StyleColor styleColor8 = new Cs.StyleColor() { Val = "auto" };

            fillReference38.Append(styleColor8);
            Cs.EffectReference effectReference38 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference38 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor117 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference38.Append(schemeColor117);

            dataPoint5.Append(lineReference38);
            dataPoint5.Append(fillReference38);
            dataPoint5.Append(effectReference38);
            dataPoint5.Append(fontReference38);

            Cs.DataPoint3D dataPoint3D2 = new Cs.DataPoint3D();
            Cs.LineReference lineReference39 = new Cs.LineReference() { Index = (UInt32Value)0U };

            Cs.FillReference fillReference39 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.StyleColor styleColor9 = new Cs.StyleColor() { Val = "auto" };

            fillReference39.Append(styleColor9);
            Cs.EffectReference effectReference39 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference39 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor118 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference39.Append(schemeColor118);

            dataPoint3D2.Append(lineReference39);
            dataPoint3D2.Append(fillReference39);
            dataPoint3D2.Append(effectReference39);
            dataPoint3D2.Append(fontReference39);

            Cs.DataPointLine dataPointLine2 = new Cs.DataPointLine();

            Cs.LineReference lineReference40 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor10 = new Cs.StyleColor() { Val = "auto" };

            lineReference40.Append(styleColor10);
            Cs.FillReference fillReference40 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.EffectReference effectReference40 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference40 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor119 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference40.Append(schemeColor119);

            Cs.ShapeProperties shapeProperties42 = new Cs.ShapeProperties();

            A.Outline outline62 = new A.Outline() { Width = 28575, CapType = A.LineCapValues.Round };

            A.SolidFill solidFill72 = new A.SolidFill();
            A.SchemeColor schemeColor120 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill72.Append(schemeColor120);
            A.Round round22 = new A.Round();

            outline62.Append(solidFill72);
            outline62.Append(round22);

            shapeProperties42.Append(outline62);

            dataPointLine2.Append(lineReference40);
            dataPointLine2.Append(fillReference40);
            dataPointLine2.Append(effectReference40);
            dataPointLine2.Append(fontReference40);
            dataPointLine2.Append(shapeProperties42);

            Cs.DataPointMarker dataPointMarker2 = new Cs.DataPointMarker();

            Cs.LineReference lineReference41 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor11 = new Cs.StyleColor() { Val = "auto" };

            lineReference41.Append(styleColor11);

            Cs.FillReference fillReference41 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.StyleColor styleColor12 = new Cs.StyleColor() { Val = "auto" };

            fillReference41.Append(styleColor12);
            Cs.EffectReference effectReference41 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference41 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor121 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference41.Append(schemeColor121);

            Cs.ShapeProperties shapeProperties43 = new Cs.ShapeProperties();

            A.Outline outline63 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill73 = new A.SolidFill();
            A.SchemeColor schemeColor122 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill73.Append(schemeColor122);

            outline63.Append(solidFill73);

            shapeProperties43.Append(outline63);

            dataPointMarker2.Append(lineReference41);
            dataPointMarker2.Append(fillReference41);
            dataPointMarker2.Append(effectReference41);
            dataPointMarker2.Append(fontReference41);
            dataPointMarker2.Append(shapeProperties43);
            Cs.MarkerLayoutProperties markerLayoutProperties2 = new Cs.MarkerLayoutProperties() { Symbol = Cs.MarkerStyle.Circle, Size = 5 };

            Cs.DataPointWireframe dataPointWireframe2 = new Cs.DataPointWireframe();

            Cs.LineReference lineReference42 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor13 = new Cs.StyleColor() { Val = "auto" };

            lineReference42.Append(styleColor13);
            Cs.FillReference fillReference42 = new Cs.FillReference() { Index = (UInt32Value)1U };
            Cs.EffectReference effectReference42 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference42 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor123 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference42.Append(schemeColor123);

            Cs.ShapeProperties shapeProperties44 = new Cs.ShapeProperties();

            A.Outline outline64 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Round };

            A.SolidFill solidFill74 = new A.SolidFill();
            A.SchemeColor schemeColor124 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill74.Append(schemeColor124);
            A.Round round23 = new A.Round();

            outline64.Append(solidFill74);
            outline64.Append(round23);

            shapeProperties44.Append(outline64);

            dataPointWireframe2.Append(lineReference42);
            dataPointWireframe2.Append(fillReference42);
            dataPointWireframe2.Append(effectReference42);
            dataPointWireframe2.Append(fontReference42);
            dataPointWireframe2.Append(shapeProperties44);

            Cs.DataTableStyle dataTableStyle2 = new Cs.DataTableStyle();
            Cs.LineReference lineReference43 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference43 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference43 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference43 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor125 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation69 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset61 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor125.Append(luminanceModulation69);
            schemeColor125.Append(luminanceOffset61);

            fontReference43.Append(schemeColor125);

            Cs.ShapeProperties shapeProperties45 = new Cs.ShapeProperties();
            A.NoFill noFill47 = new A.NoFill();

            A.Outline outline65 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill75 = new A.SolidFill();

            A.SchemeColor schemeColor126 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation70 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset62 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor126.Append(luminanceModulation70);
            schemeColor126.Append(luminanceOffset62);

            solidFill75.Append(schemeColor126);
            A.Round round24 = new A.Round();

            outline65.Append(solidFill75);
            outline65.Append(round24);

            shapeProperties45.Append(noFill47);
            shapeProperties45.Append(outline65);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType17 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            dataTableStyle2.Append(lineReference43);
            dataTableStyle2.Append(fillReference43);
            dataTableStyle2.Append(effectReference43);
            dataTableStyle2.Append(fontReference43);
            dataTableStyle2.Append(shapeProperties45);
            dataTableStyle2.Append(textCharacterPropertiesType17);

            Cs.DownBar downBar2 = new Cs.DownBar();
            Cs.LineReference lineReference44 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference44 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference44 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference44 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor127 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference44.Append(schemeColor127);

            Cs.ShapeProperties shapeProperties46 = new Cs.ShapeProperties();

            A.SolidFill solidFill76 = new A.SolidFill();

            A.SchemeColor schemeColor128 = new A.SchemeColor() { Val = A.SchemeColorValues.Dark1 };
            A.LuminanceModulation luminanceModulation71 = new A.LuminanceModulation() { Val = 75000 };
            A.LuminanceOffset luminanceOffset63 = new A.LuminanceOffset() { Val = 25000 };

            schemeColor128.Append(luminanceModulation71);
            schemeColor128.Append(luminanceOffset63);

            solidFill76.Append(schemeColor128);

            A.Outline outline66 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill77 = new A.SolidFill();

            A.SchemeColor schemeColor129 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation72 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset64 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor129.Append(luminanceModulation72);
            schemeColor129.Append(luminanceOffset64);

            solidFill77.Append(schemeColor129);
            A.Round round25 = new A.Round();

            outline66.Append(solidFill77);
            outline66.Append(round25);

            shapeProperties46.Append(solidFill76);
            shapeProperties46.Append(outline66);

            downBar2.Append(lineReference44);
            downBar2.Append(fillReference44);
            downBar2.Append(effectReference44);
            downBar2.Append(fontReference44);
            downBar2.Append(shapeProperties46);

            Cs.DropLine dropLine2 = new Cs.DropLine();
            Cs.LineReference lineReference45 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference45 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference45 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference45 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor130 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference45.Append(schemeColor130);

            Cs.ShapeProperties shapeProperties47 = new Cs.ShapeProperties();

            A.Outline outline67 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill78 = new A.SolidFill();

            A.SchemeColor schemeColor131 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation73 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset65 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor131.Append(luminanceModulation73);
            schemeColor131.Append(luminanceOffset65);

            solidFill78.Append(schemeColor131);
            A.Round round26 = new A.Round();

            outline67.Append(solidFill78);
            outline67.Append(round26);

            shapeProperties47.Append(outline67);

            dropLine2.Append(lineReference45);
            dropLine2.Append(fillReference45);
            dropLine2.Append(effectReference45);
            dropLine2.Append(fontReference45);
            dropLine2.Append(shapeProperties47);

            Cs.ErrorBar errorBar2 = new Cs.ErrorBar();
            Cs.LineReference lineReference46 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference46 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference46 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference46 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor132 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference46.Append(schemeColor132);

            Cs.ShapeProperties shapeProperties48 = new Cs.ShapeProperties();

            A.Outline outline68 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill79 = new A.SolidFill();

            A.SchemeColor schemeColor133 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation74 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset66 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor133.Append(luminanceModulation74);
            schemeColor133.Append(luminanceOffset66);

            solidFill79.Append(schemeColor133);
            A.Round round27 = new A.Round();

            outline68.Append(solidFill79);
            outline68.Append(round27);

            shapeProperties48.Append(outline68);

            errorBar2.Append(lineReference46);
            errorBar2.Append(fillReference46);
            errorBar2.Append(effectReference46);
            errorBar2.Append(fontReference46);
            errorBar2.Append(shapeProperties48);

            Cs.Floor floor3 = new Cs.Floor();
            Cs.LineReference lineReference47 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference47 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference47 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference47 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor134 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference47.Append(schemeColor134);

            Cs.ShapeProperties shapeProperties49 = new Cs.ShapeProperties();
            A.NoFill noFill48 = new A.NoFill();

            A.Outline outline69 = new A.Outline();
            A.NoFill noFill49 = new A.NoFill();

            outline69.Append(noFill49);

            shapeProperties49.Append(noFill48);
            shapeProperties49.Append(outline69);

            floor3.Append(lineReference47);
            floor3.Append(fillReference47);
            floor3.Append(effectReference47);
            floor3.Append(fontReference47);
            floor3.Append(shapeProperties49);

            Cs.GridlineMajor gridlineMajor2 = new Cs.GridlineMajor();
            Cs.LineReference lineReference48 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference48 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference48 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference48 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor135 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference48.Append(schemeColor135);

            Cs.ShapeProperties shapeProperties50 = new Cs.ShapeProperties();

            A.Outline outline70 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill80 = new A.SolidFill();

            A.SchemeColor schemeColor136 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation75 = new A.LuminanceModulation() { Val = 15000 };
            A.LuminanceOffset luminanceOffset67 = new A.LuminanceOffset() { Val = 85000 };

            schemeColor136.Append(luminanceModulation75);
            schemeColor136.Append(luminanceOffset67);

            solidFill80.Append(schemeColor136);
            A.Round round28 = new A.Round();

            outline70.Append(solidFill80);
            outline70.Append(round28);

            shapeProperties50.Append(outline70);

            gridlineMajor2.Append(lineReference48);
            gridlineMajor2.Append(fillReference48);
            gridlineMajor2.Append(effectReference48);
            gridlineMajor2.Append(fontReference48);
            gridlineMajor2.Append(shapeProperties50);

            Cs.GridlineMinor gridlineMinor2 = new Cs.GridlineMinor();
            Cs.LineReference lineReference49 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference49 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference49 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference49 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor137 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference49.Append(schemeColor137);

            Cs.ShapeProperties shapeProperties51 = new Cs.ShapeProperties();

            A.Outline outline71 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill81 = new A.SolidFill();

            A.SchemeColor schemeColor138 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation76 = new A.LuminanceModulation() { Val = 5000 };
            A.LuminanceOffset luminanceOffset68 = new A.LuminanceOffset() { Val = 95000 };

            schemeColor138.Append(luminanceModulation76);
            schemeColor138.Append(luminanceOffset68);

            solidFill81.Append(schemeColor138);
            A.Round round29 = new A.Round();

            outline71.Append(solidFill81);
            outline71.Append(round29);

            shapeProperties51.Append(outline71);

            gridlineMinor2.Append(lineReference49);
            gridlineMinor2.Append(fillReference49);
            gridlineMinor2.Append(effectReference49);
            gridlineMinor2.Append(fontReference49);
            gridlineMinor2.Append(shapeProperties51);

            Cs.HiLoLine hiLoLine2 = new Cs.HiLoLine();
            Cs.LineReference lineReference50 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference50 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference50 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference50 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor139 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference50.Append(schemeColor139);

            Cs.ShapeProperties shapeProperties52 = new Cs.ShapeProperties();

            A.Outline outline72 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill82 = new A.SolidFill();

            A.SchemeColor schemeColor140 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation77 = new A.LuminanceModulation() { Val = 50000 };
            A.LuminanceOffset luminanceOffset69 = new A.LuminanceOffset() { Val = 50000 };

            schemeColor140.Append(luminanceModulation77);
            schemeColor140.Append(luminanceOffset69);

            solidFill82.Append(schemeColor140);
            A.Round round30 = new A.Round();

            outline72.Append(solidFill82);
            outline72.Append(round30);

            shapeProperties52.Append(outline72);

            hiLoLine2.Append(lineReference50);
            hiLoLine2.Append(fillReference50);
            hiLoLine2.Append(effectReference50);
            hiLoLine2.Append(fontReference50);
            hiLoLine2.Append(shapeProperties52);

            Cs.LeaderLine leaderLine2 = new Cs.LeaderLine();
            Cs.LineReference lineReference51 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference51 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference51 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference51 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor141 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference51.Append(schemeColor141);

            Cs.ShapeProperties shapeProperties53 = new Cs.ShapeProperties();

            A.Outline outline73 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill83 = new A.SolidFill();

            A.SchemeColor schemeColor142 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation78 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset70 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor142.Append(luminanceModulation78);
            schemeColor142.Append(luminanceOffset70);

            solidFill83.Append(schemeColor142);
            A.Round round31 = new A.Round();

            outline73.Append(solidFill83);
            outline73.Append(round31);

            shapeProperties53.Append(outline73);

            leaderLine2.Append(lineReference51);
            leaderLine2.Append(fillReference51);
            leaderLine2.Append(effectReference51);
            leaderLine2.Append(fontReference51);
            leaderLine2.Append(shapeProperties53);

            Cs.LegendStyle legendStyle2 = new Cs.LegendStyle();
            Cs.LineReference lineReference52 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference52 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference52 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference52 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor143 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation79 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset71 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor143.Append(luminanceModulation79);
            schemeColor143.Append(luminanceOffset71);

            fontReference52.Append(schemeColor143);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType18 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            legendStyle2.Append(lineReference52);
            legendStyle2.Append(fillReference52);
            legendStyle2.Append(effectReference52);
            legendStyle2.Append(fontReference52);
            legendStyle2.Append(textCharacterPropertiesType18);

            Cs.PlotArea plotArea4 = new Cs.PlotArea() { Modifiers = new ListValue<StringValue>() { InnerText = "allowNoFillOverride allowNoLineOverride" } };
            Cs.LineReference lineReference53 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference53 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference53 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference53 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor144 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference53.Append(schemeColor144);

            plotArea4.Append(lineReference53);
            plotArea4.Append(fillReference53);
            plotArea4.Append(effectReference53);
            plotArea4.Append(fontReference53);

            Cs.PlotArea3D plotArea3D2 = new Cs.PlotArea3D() { Modifiers = new ListValue<StringValue>() { InnerText = "allowNoFillOverride allowNoLineOverride" } };
            Cs.LineReference lineReference54 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference54 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference54 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference54 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor145 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference54.Append(schemeColor145);

            plotArea3D2.Append(lineReference54);
            plotArea3D2.Append(fillReference54);
            plotArea3D2.Append(effectReference54);
            plotArea3D2.Append(fontReference54);

            Cs.SeriesAxis seriesAxis2 = new Cs.SeriesAxis();
            Cs.LineReference lineReference55 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference55 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference55 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference55 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor146 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation80 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset72 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor146.Append(luminanceModulation80);
            schemeColor146.Append(luminanceOffset72);

            fontReference55.Append(schemeColor146);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType19 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            seriesAxis2.Append(lineReference55);
            seriesAxis2.Append(fillReference55);
            seriesAxis2.Append(effectReference55);
            seriesAxis2.Append(fontReference55);
            seriesAxis2.Append(textCharacterPropertiesType19);

            Cs.SeriesLine seriesLine2 = new Cs.SeriesLine();
            Cs.LineReference lineReference56 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference56 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference56 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference56 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor147 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference56.Append(schemeColor147);

            Cs.ShapeProperties shapeProperties54 = new Cs.ShapeProperties();

            A.Outline outline74 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill84 = new A.SolidFill();

            A.SchemeColor schemeColor148 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation81 = new A.LuminanceModulation() { Val = 35000 };
            A.LuminanceOffset luminanceOffset73 = new A.LuminanceOffset() { Val = 65000 };

            schemeColor148.Append(luminanceModulation81);
            schemeColor148.Append(luminanceOffset73);

            solidFill84.Append(schemeColor148);
            A.Round round32 = new A.Round();

            outline74.Append(solidFill84);
            outline74.Append(round32);

            shapeProperties54.Append(outline74);

            seriesLine2.Append(lineReference56);
            seriesLine2.Append(fillReference56);
            seriesLine2.Append(effectReference56);
            seriesLine2.Append(fontReference56);
            seriesLine2.Append(shapeProperties54);

            Cs.TitleStyle titleStyle2 = new Cs.TitleStyle();
            Cs.LineReference lineReference57 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference57 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference57 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference57 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor149 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation82 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset74 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor149.Append(luminanceModulation82);
            schemeColor149.Append(luminanceOffset74);

            fontReference57.Append(schemeColor149);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType20 = new Cs.TextCharacterPropertiesType() { FontSize = 1400, Bold = false, Kerning = 1200, Spacing = 0, Baseline = 0 };

            titleStyle2.Append(lineReference57);
            titleStyle2.Append(fillReference57);
            titleStyle2.Append(effectReference57);
            titleStyle2.Append(fontReference57);
            titleStyle2.Append(textCharacterPropertiesType20);

            Cs.TrendlineStyle trendlineStyle2 = new Cs.TrendlineStyle();

            Cs.LineReference lineReference58 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.StyleColor styleColor14 = new Cs.StyleColor() { Val = "auto" };

            lineReference58.Append(styleColor14);
            Cs.FillReference fillReference58 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference58 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference58 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor150 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference58.Append(schemeColor150);

            Cs.ShapeProperties shapeProperties55 = new Cs.ShapeProperties();

            A.Outline outline75 = new A.Outline() { Width = 19050, CapType = A.LineCapValues.Round };

            A.SolidFill solidFill85 = new A.SolidFill();
            A.SchemeColor schemeColor151 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill85.Append(schemeColor151);
            A.PresetDash presetDash2 = new A.PresetDash() { Val = A.PresetLineDashValues.SystemDot };

            outline75.Append(solidFill85);
            outline75.Append(presetDash2);

            shapeProperties55.Append(outline75);

            trendlineStyle2.Append(lineReference58);
            trendlineStyle2.Append(fillReference58);
            trendlineStyle2.Append(effectReference58);
            trendlineStyle2.Append(fontReference58);
            trendlineStyle2.Append(shapeProperties55);

            Cs.TrendlineLabel trendlineLabel2 = new Cs.TrendlineLabel();
            Cs.LineReference lineReference59 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference59 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference59 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference59 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor152 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation83 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset75 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor152.Append(luminanceModulation83);
            schemeColor152.Append(luminanceOffset75);

            fontReference59.Append(schemeColor152);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType21 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            trendlineLabel2.Append(lineReference59);
            trendlineLabel2.Append(fillReference59);
            trendlineLabel2.Append(effectReference59);
            trendlineLabel2.Append(fontReference59);
            trendlineLabel2.Append(textCharacterPropertiesType21);

            Cs.UpBar upBar2 = new Cs.UpBar();
            Cs.LineReference lineReference60 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference60 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference60 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference60 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor153 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference60.Append(schemeColor153);

            Cs.ShapeProperties shapeProperties56 = new Cs.ShapeProperties();

            A.SolidFill solidFill86 = new A.SolidFill();
            A.SchemeColor schemeColor154 = new A.SchemeColor() { Val = A.SchemeColorValues.Light1 };

            solidFill86.Append(schemeColor154);

            A.Outline outline76 = new A.Outline() { Width = 9525, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill87 = new A.SolidFill();

            A.SchemeColor schemeColor155 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation84 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset76 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor155.Append(luminanceModulation84);
            schemeColor155.Append(luminanceOffset76);

            solidFill87.Append(schemeColor155);
            A.Round round33 = new A.Round();

            outline76.Append(solidFill87);
            outline76.Append(round33);

            shapeProperties56.Append(solidFill86);
            shapeProperties56.Append(outline76);

            upBar2.Append(lineReference60);
            upBar2.Append(fillReference60);
            upBar2.Append(effectReference60);
            upBar2.Append(fontReference60);
            upBar2.Append(shapeProperties56);

            Cs.ValueAxis valueAxis3 = new Cs.ValueAxis();
            Cs.LineReference lineReference61 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference61 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference61 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference61 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };

            A.SchemeColor schemeColor156 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };
            A.LuminanceModulation luminanceModulation85 = new A.LuminanceModulation() { Val = 65000 };
            A.LuminanceOffset luminanceOffset77 = new A.LuminanceOffset() { Val = 35000 };

            schemeColor156.Append(luminanceModulation85);
            schemeColor156.Append(luminanceOffset77);

            fontReference61.Append(schemeColor156);
            Cs.TextCharacterPropertiesType textCharacterPropertiesType22 = new Cs.TextCharacterPropertiesType() { FontSize = 900, Kerning = 1200 };

            valueAxis3.Append(lineReference61);
            valueAxis3.Append(fillReference61);
            valueAxis3.Append(effectReference61);
            valueAxis3.Append(fontReference61);
            valueAxis3.Append(textCharacterPropertiesType22);

            Cs.Wall wall2 = new Cs.Wall();
            Cs.LineReference lineReference62 = new Cs.LineReference() { Index = (UInt32Value)0U };
            Cs.FillReference fillReference62 = new Cs.FillReference() { Index = (UInt32Value)0U };
            Cs.EffectReference effectReference62 = new Cs.EffectReference() { Index = (UInt32Value)0U };

            Cs.FontReference fontReference62 = new Cs.FontReference() { Index = A.FontCollectionIndexValues.Minor };
            A.SchemeColor schemeColor157 = new A.SchemeColor() { Val = A.SchemeColorValues.Text1 };

            fontReference62.Append(schemeColor157);

            Cs.ShapeProperties shapeProperties57 = new Cs.ShapeProperties();
            A.NoFill noFill50 = new A.NoFill();

            A.Outline outline77 = new A.Outline();
            A.NoFill noFill51 = new A.NoFill();

            outline77.Append(noFill51);

            shapeProperties57.Append(noFill50);
            shapeProperties57.Append(outline77);

            wall2.Append(lineReference62);
            wall2.Append(fillReference62);
            wall2.Append(effectReference62);
            wall2.Append(fontReference62);
            wall2.Append(shapeProperties57);

            chartStyle2.Append(axisTitle2);
            chartStyle2.Append(categoryAxis3);
            chartStyle2.Append(chartArea2);
            chartStyle2.Append(dataLabel6);
            chartStyle2.Append(dataLabelCallout2);
            chartStyle2.Append(dataPoint5);
            chartStyle2.Append(dataPoint3D2);
            chartStyle2.Append(dataPointLine2);
            chartStyle2.Append(dataPointMarker2);
            chartStyle2.Append(markerLayoutProperties2);
            chartStyle2.Append(dataPointWireframe2);
            chartStyle2.Append(dataTableStyle2);
            chartStyle2.Append(downBar2);
            chartStyle2.Append(dropLine2);
            chartStyle2.Append(errorBar2);
            chartStyle2.Append(floor3);
            chartStyle2.Append(gridlineMajor2);
            chartStyle2.Append(gridlineMinor2);
            chartStyle2.Append(hiLoLine2);
            chartStyle2.Append(leaderLine2);
            chartStyle2.Append(legendStyle2);
            chartStyle2.Append(plotArea4);
            chartStyle2.Append(plotArea3D2);
            chartStyle2.Append(seriesAxis2);
            chartStyle2.Append(seriesLine2);
            chartStyle2.Append(titleStyle2);
            chartStyle2.Append(trendlineStyle2);
            chartStyle2.Append(trendlineLabel2);
            chartStyle2.Append(upBar2);
            chartStyle2.Append(valueAxis3);
            chartStyle2.Append(wall2);

            chartStylePart2.ChartStyle = chartStyle2;
        }

        // Generates content of imagePart2.
        private void generateImageCoverLarge(ImagePart coverLargePart)
        {
            System.IO.Stream data = GetBinaryDataStream(coverLargePartData);
            coverLargePart.FeedData(data);
            data.Close();
        }
        // Generates content of imagePart2.
        private void GenerateImagePart2Content(ImagePart imagePart2)
        {
            System.IO.Stream data = GetBinaryDataStream(imagePart2Data);
            imagePart2.FeedData(data);
            data.Close();
        }

        // Generates content of workbookStylesPart1.
        private void GenerateWorkbookStylesPart1Content(WorkbookStylesPart workbookStylesPart1)
        {
            Stylesheet stylesheet1 = new Stylesheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac x16r2" } };
            stylesheet1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            stylesheet1.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
            stylesheet1.AddNamespaceDeclaration("x16r2", "http://schemas.microsoft.com/office/spreadsheetml/2015/02/main");
            //stylesheet1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");

            Fonts fonts1 = new Fonts() { Count = (UInt32Value)1U, KnownFonts = true };

            Font font1 = new Font();
            FontSize fontSize1 = new FontSize() { Val = 11D };
            Color color1 = new Color() { Theme = (UInt32Value)1U };
            FontName fontName1 = new FontName() { Val = "Calibri" };
            FontFamilyNumbering fontFamilyNumbering1 = new FontFamilyNumbering() { Val = 2 };
            FontScheme fontScheme1 = new FontScheme() { Val = FontSchemeValues.Minor };

            font1.Append(fontSize1);
            font1.Append(color1);
            font1.Append(fontName1);
            font1.Append(fontFamilyNumbering1);
            font1.Append(fontScheme1);

            fonts1.Append(font1);

            Fills fills1 = new Fills() { Count = (UInt32Value)3U };

            Fill fill1 = new Fill();
            PatternFill patternFill1 = new PatternFill() { PatternType = PatternValues.None };

            fill1.Append(patternFill1);

            Fill fill2 = new Fill();
            PatternFill patternFill2 = new PatternFill() { PatternType = PatternValues.Gray125 };

            fill2.Append(patternFill2);

            Fill fill3 = new Fill();

            PatternFill patternFill3 = new PatternFill() { PatternType = PatternValues.Solid };
            ForegroundColor foregroundColor1 = new ForegroundColor() { Theme = (UInt32Value)2U };
            BackgroundColor backgroundColor1 = new BackgroundColor() { Indexed = (UInt32Value)64U };

            patternFill3.Append(foregroundColor1);
            patternFill3.Append(backgroundColor1);

            fill3.Append(patternFill3);

            fills1.Append(fill1);
            fills1.Append(fill2);
            fills1.Append(fill3);

            Borders borders1 = new Borders() { Count = (UInt32Value)9U };

            Border border1 = new Border();
            LeftBorder leftBorder1 = new LeftBorder();
            RightBorder rightBorder1 = new RightBorder();
            TopBorder topBorder1 = new TopBorder();
            BottomBorder bottomBorder1 = new BottomBorder();
            DiagonalBorder diagonalBorder1 = new DiagonalBorder();

            border1.Append(leftBorder1);
            border1.Append(rightBorder1);
            border1.Append(topBorder1);
            border1.Append(bottomBorder1);
            border1.Append(diagonalBorder1);

            Border border2 = new Border();

            LeftBorder leftBorder2 = new LeftBorder() { Style = BorderStyleValues.Thin };
            Color color2 = new Color() { Indexed = (UInt32Value)64U };

            leftBorder2.Append(color2);
            RightBorder rightBorder2 = new RightBorder();

            TopBorder topBorder2 = new TopBorder() { Style = BorderStyleValues.Thin };
            Color color3 = new Color() { Indexed = (UInt32Value)64U };

            topBorder2.Append(color3);
            BottomBorder bottomBorder2 = new BottomBorder();
            DiagonalBorder diagonalBorder2 = new DiagonalBorder();

            border2.Append(leftBorder2);
            border2.Append(rightBorder2);
            border2.Append(topBorder2);
            border2.Append(bottomBorder2);
            border2.Append(diagonalBorder2);

            Border border3 = new Border();
            LeftBorder leftBorder3 = new LeftBorder();
            RightBorder rightBorder3 = new RightBorder();

            TopBorder topBorder3 = new TopBorder() { Style = BorderStyleValues.Thin };
            Color color4 = new Color() { Indexed = (UInt32Value)64U };

            topBorder3.Append(color4);
            BottomBorder bottomBorder3 = new BottomBorder();
            DiagonalBorder diagonalBorder3 = new DiagonalBorder();

            border3.Append(leftBorder3);
            border3.Append(rightBorder3);
            border3.Append(topBorder3);
            border3.Append(bottomBorder3);
            border3.Append(diagonalBorder3);

            Border border4 = new Border();
            LeftBorder leftBorder4 = new LeftBorder();

            RightBorder rightBorder4 = new RightBorder() { Style = BorderStyleValues.Thin };
            Color color5 = new Color() { Indexed = (UInt32Value)64U };

            rightBorder4.Append(color5);

            TopBorder topBorder4 = new TopBorder() { Style = BorderStyleValues.Thin };
            Color color6 = new Color() { Indexed = (UInt32Value)64U };

            topBorder4.Append(color6);
            BottomBorder bottomBorder4 = new BottomBorder();
            DiagonalBorder diagonalBorder4 = new DiagonalBorder();

            border4.Append(leftBorder4);
            border4.Append(rightBorder4);
            border4.Append(topBorder4);
            border4.Append(bottomBorder4);
            border4.Append(diagonalBorder4);

            Border border5 = new Border();

            LeftBorder leftBorder5 = new LeftBorder() { Style = BorderStyleValues.Thin };
            Color color7 = new Color() { Indexed = (UInt32Value)64U };

            leftBorder5.Append(color7);
            RightBorder rightBorder5 = new RightBorder();
            TopBorder topBorder5 = new TopBorder();
            BottomBorder bottomBorder5 = new BottomBorder();
            DiagonalBorder diagonalBorder5 = new DiagonalBorder();

            border5.Append(leftBorder5);
            border5.Append(rightBorder5);
            border5.Append(topBorder5);
            border5.Append(bottomBorder5);
            border5.Append(diagonalBorder5);

            Border border6 = new Border();
            LeftBorder leftBorder6 = new LeftBorder();

            RightBorder rightBorder6 = new RightBorder() { Style = BorderStyleValues.Thin };
            Color color8 = new Color() { Indexed = (UInt32Value)64U };

            rightBorder6.Append(color8);
            TopBorder topBorder6 = new TopBorder();
            BottomBorder bottomBorder6 = new BottomBorder();
            DiagonalBorder diagonalBorder6 = new DiagonalBorder();

            border6.Append(leftBorder6);
            border6.Append(rightBorder6);
            border6.Append(topBorder6);
            border6.Append(bottomBorder6);
            border6.Append(diagonalBorder6);

            Border border7 = new Border();

            LeftBorder leftBorder7 = new LeftBorder() { Style = BorderStyleValues.Thin };
            Color color9 = new Color() { Indexed = (UInt32Value)64U };

            leftBorder7.Append(color9);
            RightBorder rightBorder7 = new RightBorder();
            TopBorder topBorder7 = new TopBorder();

            BottomBorder bottomBorder7 = new BottomBorder() { Style = BorderStyleValues.Thin };
            Color color10 = new Color() { Indexed = (UInt32Value)64U };

            bottomBorder7.Append(color10);
            DiagonalBorder diagonalBorder7 = new DiagonalBorder();

            border7.Append(leftBorder7);
            border7.Append(rightBorder7);
            border7.Append(topBorder7);
            border7.Append(bottomBorder7);
            border7.Append(diagonalBorder7);

            Border border8 = new Border();
            LeftBorder leftBorder8 = new LeftBorder();
            RightBorder rightBorder8 = new RightBorder();
            TopBorder topBorder8 = new TopBorder();

            BottomBorder bottomBorder8 = new BottomBorder() { Style = BorderStyleValues.Thin };
            Color color11 = new Color() { Indexed = (UInt32Value)64U };

            bottomBorder8.Append(color11);
            DiagonalBorder diagonalBorder8 = new DiagonalBorder();

            border8.Append(leftBorder8);
            border8.Append(rightBorder8);
            border8.Append(topBorder8);
            border8.Append(bottomBorder8);
            border8.Append(diagonalBorder8);

            Border border9 = new Border();
            LeftBorder leftBorder9 = new LeftBorder();

            RightBorder rightBorder9 = new RightBorder() { Style = BorderStyleValues.Thin };
            Color color12 = new Color() { Indexed = (UInt32Value)64U };

            rightBorder9.Append(color12);
            TopBorder topBorder9 = new TopBorder();

            BottomBorder bottomBorder9 = new BottomBorder() { Style = BorderStyleValues.Thin };
            Color color13 = new Color() { Indexed = (UInt32Value)64U };

            bottomBorder9.Append(color13);
            DiagonalBorder diagonalBorder9 = new DiagonalBorder();

            border9.Append(leftBorder9);
            border9.Append(rightBorder9);
            border9.Append(topBorder9);
            border9.Append(bottomBorder9);
            border9.Append(diagonalBorder9);

            borders1.Append(border1);
            borders1.Append(border2);
            borders1.Append(border3);
            borders1.Append(border4);
            borders1.Append(border5);
            borders1.Append(border6);
            borders1.Append(border7);
            borders1.Append(border8);
            borders1.Append(border9);

            CellStyleFormats cellStyleFormats1 = new CellStyleFormats() { Count = (UInt32Value)1U };
            CellFormat cellFormat1 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U };

            cellStyleFormats1.Append(cellFormat1);

            CellFormats cellFormats1 = new CellFormats() { Count = (UInt32Value)13U };
            CellFormat cellFormat2 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U };
            CellFormat cellFormat3 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, PivotButton = true };

            CellFormat cellFormat4 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyAlignment = true };
            Alignment alignment1 = new Alignment() { Horizontal = HorizontalAlignmentValues.Left };

            cellFormat4.Append(alignment1);
            CellFormat cellFormat5 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true };
            CellFormat cellFormat6 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)1U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat7 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)2U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat8 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)3U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat9 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)4U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat10 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat11 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)5U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat12 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)6U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat13 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)7U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };
            CellFormat cellFormat14 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)2U, BorderId = (UInt32Value)8U, FormatId = (UInt32Value)0U, ApplyFill = true, ApplyBorder = true };

            cellFormats1.Append(cellFormat2);
            cellFormats1.Append(cellFormat3);
            cellFormats1.Append(cellFormat4);
            cellFormats1.Append(cellFormat5);
            cellFormats1.Append(cellFormat6);
            cellFormats1.Append(cellFormat7);
            cellFormats1.Append(cellFormat8);
            cellFormats1.Append(cellFormat9);
            cellFormats1.Append(cellFormat10);
            cellFormats1.Append(cellFormat11);
            cellFormats1.Append(cellFormat12);
            cellFormats1.Append(cellFormat13);
            cellFormats1.Append(cellFormat14);

            CellStyles cellStyles1 = new CellStyles() { Count = (UInt32Value)1U };
            CellStyle cellStyle1 = new CellStyle() { Name = "Normal", FormatId = (UInt32Value)0U, BuiltinId = (UInt32Value)0U };

            cellStyles1.Append(cellStyle1);
            DifferentialFormats differentialFormats1 = new DifferentialFormats() { Count = (UInt32Value)0U };
            TableStyles tableStyles1 = new TableStyles() { Count = (UInt32Value)0U, DefaultTableStyle = "TableStyleMedium2", DefaultPivotStyle = "PivotStyleMedium9" };

            Colors colors1 = new Colors();

            MruColors mruColors1 = new MruColors();
            Color color14 = new Color() { Rgb = "FFFF4B4B" };

            mruColors1.Append(color14);

            colors1.Append(mruColors1);

            StylesheetExtensionList stylesheetExtensionList1 = new StylesheetExtensionList();

            StylesheetExtension stylesheetExtension1 = new StylesheetExtension() { Uri = "{EB79DEF2-80B8-43e5-95BD-54CBDDF9020C}" };
            stylesheetExtension1.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
            X14.SlicerStyles slicerStyles1 = new X14.SlicerStyles() { DefaultSlicerStyle = "SlicerStyleLight1" };

            stylesheetExtension1.Append(slicerStyles1);

            StylesheetExtension stylesheetExtension2 = new StylesheetExtension() { Uri = "{9260A510-F301-46a8-8635-F512D64BE5F5}" };
            stylesheetExtension2.AddNamespaceDeclaration("x15", "http://schemas.microsoft.com/office/spreadsheetml/2010/11/main");
            X15.TimelineStyles timelineStyles1 = new X15.TimelineStyles() { DefaultTimelineStyle = "TimeSlicerStyleLight1" };

            stylesheetExtension2.Append(timelineStyles1);

            stylesheetExtensionList1.Append(stylesheetExtension1);
            stylesheetExtensionList1.Append(stylesheetExtension2);

            stylesheet1.Append(fonts1);
            stylesheet1.Append(fills1);
            stylesheet1.Append(borders1);
            stylesheet1.Append(cellStyleFormats1);
            stylesheet1.Append(cellFormats1);
            stylesheet1.Append(cellStyles1);
            stylesheet1.Append(differentialFormats1);
            stylesheet1.Append(tableStyles1);
            stylesheet1.Append(colors1);
            stylesheet1.Append(stylesheetExtensionList1);

            workbookStylesPart1.Stylesheet = stylesheet1;
        }

        // Generates content of worksheetPart2.
        private void GenerateWorksheetPart2Content(WorksheetPart worksheetPart2)
        {
            Worksheet worksheet2 = new Worksheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac" } };
            worksheet2.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            worksheet2.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            worksheet2.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
            //worksheet2.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //worksheet2.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
            //worksheet2.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //worksheet2.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{8D70A270-F130-4810-944E-FF1D5DFB766F}"));
            SheetDimension sheetDimension2 = new SheetDimension() { Reference = "A1" };

            SheetViews sheetViews2 = new SheetViews();
            SheetView sheetView2 = new SheetView() { WorkbookViewId = (UInt32Value)0U };

            sheetViews2.Append(sheetView2);
            SheetFormatProperties sheetFormatProperties2 = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

            Columns columns2 = new Columns();
            Column column14 = new Column() { Min = (UInt32Value)1U, Max = (UInt32Value)1U, Width = 21.5703125D, BestFit = true, CustomWidth = true };
            Column column15 = new Column() { Min = (UInt32Value)2U, Max = (UInt32Value)2U, Width = 12.85546875D, BestFit = true, CustomWidth = true };

            columns2.Append(column14);
            columns2.Append(column15);
            SheetData sheetData2 = new SheetData();
            PageMargins pageMargins4 = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            PageSetup pageSetup4 = new PageSetup() { Orientation = OrientationValues.Portrait, Id = "rId1" };

            worksheet2.Append(sheetDimension2);
            worksheet2.Append(sheetViews2);
            worksheet2.Append(sheetFormatProperties2);
            worksheet2.Append(columns2);
            worksheet2.Append(sheetData2);
            worksheet2.Append(pageMargins4);
            worksheet2.Append(pageSetup4);

            worksheetPart2.Worksheet = worksheet2;
        }

        // Generates content of spreadsheetPrinterSettingsPart2.
        //private void GenerateSpreadsheetPrinterSettingsPart2Content(SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart2)
        //{
        //    System.IO.Stream data = GetBinaryDataStream(spreadsheetPrinterSettingsPart2Data);
        //    spreadsheetPrinterSettingsPart2.FeedData(data);
        //    data.Close();
        //}

        // Generates content of worksheetPart3.
        private void GenerateWorksheetPart3Content(WorksheetPart worksheetPart3)
        {
            Worksheet worksheet3 = new Worksheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac" } };
            worksheet3.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            worksheet3.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            worksheet3.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
            //worksheet3.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //worksheet3.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
            //worksheet3.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //worksheet3.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{FF86A848-0B39-4C6E-87E6-BCB3A09524E6}"));
            SheetDimension sheetDimension3 = new SheetDimension() { Reference = "A1:B2" };

            SheetViews sheetViews3 = new SheetViews();
            SheetView sheetView3 = new SheetView() { WorkbookViewId = (UInt32Value)0U };

            sheetViews3.Append(sheetView3);
            SheetFormatProperties sheetFormatProperties3 = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

            Columns columns3 = new Columns();
            Column column16 = new Column() { Min = (UInt32Value)2U, Max = (UInt32Value)2U, Width = 35.85546875D, BestFit = true, CustomWidth = true };
            Column column17 = new Column() { Min = (UInt32Value)3U, Max = (UInt32Value)9U, Width = 11.28515625D, BestFit = true, CustomWidth = true };

            columns3.Append(column16);
            columns3.Append(column17);

            SheetData sheetData3 = new SheetData();

            Row row37 = new Row() { RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:2" }, DyDescent = 0.25D };

            Cell cell428 = new Cell() { CellReference = "A1", DataType = CellValues.SharedString };
            CellValue cellValue34 = new CellValue();
            cellValue34.Text = "4";

            cell428.Append(cellValue34);

            Cell cell429 = new Cell() { CellReference = "B1", DataType = CellValues.SharedString };
            CellValue cellValue35 = new CellValue();
            cellValue35.Text = "5";

            cell429.Append(cellValue35);

            row37.Append(cell428);
            row37.Append(cell429);

            Row row38 = new Row() { RowIndex = (UInt32Value)2U, Spans = new ListValue<StringValue>() { InnerText = "1:2" }, DyDescent = 0.25D };

            Cell cell430 = new Cell() { CellReference = "A2", DataType = CellValues.SharedString };
            CellValue cellValue36 = new CellValue();
            cellValue36.Text = "6";

            cell430.Append(cellValue36);

            Cell cell431 = new Cell() { CellReference = "B2", DataType = CellValues.SharedString };
            CellValue cellValue37 = new CellValue();
            cellValue37.Text = "7";

            cell431.Append(cellValue37);

            row38.Append(cell430);
            row38.Append(cell431);

            sheetData3.Append(row37);
            sheetData3.Append(row38);
            PageMargins pageMargins5 = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            PageSetup pageSetup5 = new PageSetup() { Orientation = OrientationValues.Portrait, Id = "rId1" };

            worksheet3.Append(sheetDimension3);
            worksheet3.Append(sheetViews3);
            worksheet3.Append(sheetFormatProperties3);
            worksheet3.Append(columns3);
            worksheet3.Append(sheetData3);
            worksheet3.Append(pageMargins5);
            worksheet3.Append(pageSetup5);

            worksheetPart3.Worksheet = worksheet3;
        }

        // Generates content of spreadsheetPrinterSettingsPart3.
        //private void GenerateSpreadsheetPrinterSettingsPart3Content(SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart3)
        //{
        //    System.IO.Stream data = GetBinaryDataStream(spreadsheetPrinterSettingsPart3Data);
        //    spreadsheetPrinterSettingsPart3.FeedData(data);
        //    data.Close();
        //}

        // Generates content of themePart1.
        private void GenerateThemePart1Content(ThemePart themePart1)
        {
            A.Theme theme1 = new A.Theme() { Name = "Office Theme" };
            theme1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            A.ThemeElements themeElements1 = new A.ThemeElements();

            A.ColorScheme colorScheme1 = new A.ColorScheme() { Name = "Office" };

            A.Dark1Color dark1Color1 = new A.Dark1Color();
            A.SystemColor systemColor1 = new A.SystemColor() { Val = A.SystemColorValues.WindowText, LastColor = "000000" };

            dark1Color1.Append(systemColor1);

            A.Light1Color light1Color1 = new A.Light1Color();
            A.SystemColor systemColor2 = new A.SystemColor() { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" };

            light1Color1.Append(systemColor2);

            A.Dark2Color dark2Color1 = new A.Dark2Color();
            A.RgbColorModelHex rgbColorModelHex5 = new A.RgbColorModelHex() { Val = "44546A" };

            dark2Color1.Append(rgbColorModelHex5);

            A.Light2Color light2Color1 = new A.Light2Color();
            A.RgbColorModelHex rgbColorModelHex6 = new A.RgbColorModelHex() { Val = "E7E6E6" };

            light2Color1.Append(rgbColorModelHex6);

            A.Accent1Color accent1Color1 = new A.Accent1Color();
            A.RgbColorModelHex rgbColorModelHex7 = new A.RgbColorModelHex() { Val = "4472C4" };

            accent1Color1.Append(rgbColorModelHex7);

            A.Accent2Color accent2Color1 = new A.Accent2Color();
            A.RgbColorModelHex rgbColorModelHex8 = new A.RgbColorModelHex() { Val = "ED7D31" };

            accent2Color1.Append(rgbColorModelHex8);

            A.Accent3Color accent3Color1 = new A.Accent3Color();
            A.RgbColorModelHex rgbColorModelHex9 = new A.RgbColorModelHex() { Val = "A5A5A5" };

            accent3Color1.Append(rgbColorModelHex9);

            A.Accent4Color accent4Color1 = new A.Accent4Color();
            A.RgbColorModelHex rgbColorModelHex10 = new A.RgbColorModelHex() { Val = "FFC000" };

            accent4Color1.Append(rgbColorModelHex10);

            A.Accent5Color accent5Color1 = new A.Accent5Color();
            A.RgbColorModelHex rgbColorModelHex11 = new A.RgbColorModelHex() { Val = "5B9BD5" };

            accent5Color1.Append(rgbColorModelHex11);

            A.Accent6Color accent6Color1 = new A.Accent6Color();
            A.RgbColorModelHex rgbColorModelHex12 = new A.RgbColorModelHex() { Val = "70AD47" };

            accent6Color1.Append(rgbColorModelHex12);

            A.Hyperlink hyperlink1 = new A.Hyperlink();
            A.RgbColorModelHex rgbColorModelHex13 = new A.RgbColorModelHex() { Val = "0563C1" };

            hyperlink1.Append(rgbColorModelHex13);

            A.FollowedHyperlinkColor followedHyperlinkColor1 = new A.FollowedHyperlinkColor();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = "954F72" };

            followedHyperlinkColor1.Append(rgbColorModelHex14);

            colorScheme1.Append(dark1Color1);
            colorScheme1.Append(light1Color1);
            colorScheme1.Append(dark2Color1);
            colorScheme1.Append(light2Color1);
            colorScheme1.Append(accent1Color1);
            colorScheme1.Append(accent2Color1);
            colorScheme1.Append(accent3Color1);
            colorScheme1.Append(accent4Color1);
            colorScheme1.Append(accent5Color1);
            colorScheme1.Append(accent6Color1);
            colorScheme1.Append(hyperlink1);
            colorScheme1.Append(followedHyperlinkColor1);

            A.FontScheme fontScheme2 = new A.FontScheme() { Name = "Office" };

            A.MajorFont majorFont1 = new A.MajorFont();
            A.LatinFont latinFont14 = new A.LatinFont() { Typeface = "Calibri Light", Panose = "020F0302020204030204" };
            A.EastAsianFont eastAsianFont14 = new A.EastAsianFont() { Typeface = "" };
            A.ComplexScriptFont complexScriptFont14 = new A.ComplexScriptFont() { Typeface = "" };
            A.SupplementalFont supplementalFont1 = new A.SupplementalFont() { Script = "Jpan", Typeface = "游ゴシック Light" };
            A.SupplementalFont supplementalFont2 = new A.SupplementalFont() { Script = "Hang", Typeface = "맑은 고딕" };
            A.SupplementalFont supplementalFont3 = new A.SupplementalFont() { Script = "Hans", Typeface = "等线 Light" };
            A.SupplementalFont supplementalFont4 = new A.SupplementalFont() { Script = "Hant", Typeface = "新細明體" };
            A.SupplementalFont supplementalFont5 = new A.SupplementalFont() { Script = "Arab", Typeface = "Times New Roman" };
            A.SupplementalFont supplementalFont6 = new A.SupplementalFont() { Script = "Hebr", Typeface = "Times New Roman" };
            A.SupplementalFont supplementalFont7 = new A.SupplementalFont() { Script = "Thai", Typeface = "Tahoma" };
            A.SupplementalFont supplementalFont8 = new A.SupplementalFont() { Script = "Ethi", Typeface = "Nyala" };
            A.SupplementalFont supplementalFont9 = new A.SupplementalFont() { Script = "Beng", Typeface = "Vrinda" };
            A.SupplementalFont supplementalFont10 = new A.SupplementalFont() { Script = "Gujr", Typeface = "Shruti" };
            A.SupplementalFont supplementalFont11 = new A.SupplementalFont() { Script = "Khmr", Typeface = "MoolBoran" };
            A.SupplementalFont supplementalFont12 = new A.SupplementalFont() { Script = "Knda", Typeface = "Tunga" };
            A.SupplementalFont supplementalFont13 = new A.SupplementalFont() { Script = "Guru", Typeface = "Raavi" };
            A.SupplementalFont supplementalFont14 = new A.SupplementalFont() { Script = "Cans", Typeface = "Euphemia" };
            A.SupplementalFont supplementalFont15 = new A.SupplementalFont() { Script = "Cher", Typeface = "Plantagenet Cherokee" };
            A.SupplementalFont supplementalFont16 = new A.SupplementalFont() { Script = "Yiii", Typeface = "Microsoft Yi Baiti" };
            A.SupplementalFont supplementalFont17 = new A.SupplementalFont() { Script = "Tibt", Typeface = "Microsoft Himalaya" };
            A.SupplementalFont supplementalFont18 = new A.SupplementalFont() { Script = "Thaa", Typeface = "MV Boli" };
            A.SupplementalFont supplementalFont19 = new A.SupplementalFont() { Script = "Deva", Typeface = "Mangal" };
            A.SupplementalFont supplementalFont20 = new A.SupplementalFont() { Script = "Telu", Typeface = "Gautami" };
            A.SupplementalFont supplementalFont21 = new A.SupplementalFont() { Script = "Taml", Typeface = "Latha" };
            A.SupplementalFont supplementalFont22 = new A.SupplementalFont() { Script = "Syrc", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont23 = new A.SupplementalFont() { Script = "Orya", Typeface = "Kalinga" };
            A.SupplementalFont supplementalFont24 = new A.SupplementalFont() { Script = "Mlym", Typeface = "Kartika" };
            A.SupplementalFont supplementalFont25 = new A.SupplementalFont() { Script = "Laoo", Typeface = "DokChampa" };
            A.SupplementalFont supplementalFont26 = new A.SupplementalFont() { Script = "Sinh", Typeface = "Iskoola Pota" };
            A.SupplementalFont supplementalFont27 = new A.SupplementalFont() { Script = "Mong", Typeface = "Mongolian Baiti" };
            A.SupplementalFont supplementalFont28 = new A.SupplementalFont() { Script = "Viet", Typeface = "Times New Roman" };
            A.SupplementalFont supplementalFont29 = new A.SupplementalFont() { Script = "Uigh", Typeface = "Microsoft Uighur" };
            A.SupplementalFont supplementalFont30 = new A.SupplementalFont() { Script = "Geor", Typeface = "Sylfaen" };
            A.SupplementalFont supplementalFont31 = new A.SupplementalFont() { Script = "Armn", Typeface = "Arial" };
            A.SupplementalFont supplementalFont32 = new A.SupplementalFont() { Script = "Bugi", Typeface = "Leelawadee UI" };
            A.SupplementalFont supplementalFont33 = new A.SupplementalFont() { Script = "Bopo", Typeface = "Microsoft JhengHei" };
            A.SupplementalFont supplementalFont34 = new A.SupplementalFont() { Script = "Java", Typeface = "Javanese Text" };
            A.SupplementalFont supplementalFont35 = new A.SupplementalFont() { Script = "Lisu", Typeface = "Segoe UI" };
            A.SupplementalFont supplementalFont36 = new A.SupplementalFont() { Script = "Mymr", Typeface = "Myanmar Text" };
            A.SupplementalFont supplementalFont37 = new A.SupplementalFont() { Script = "Nkoo", Typeface = "Ebrima" };
            A.SupplementalFont supplementalFont38 = new A.SupplementalFont() { Script = "Olck", Typeface = "Nirmala UI" };
            A.SupplementalFont supplementalFont39 = new A.SupplementalFont() { Script = "Osma", Typeface = "Ebrima" };
            A.SupplementalFont supplementalFont40 = new A.SupplementalFont() { Script = "Phag", Typeface = "Phagspa" };
            A.SupplementalFont supplementalFont41 = new A.SupplementalFont() { Script = "Syrn", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont42 = new A.SupplementalFont() { Script = "Syrj", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont43 = new A.SupplementalFont() { Script = "Syre", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont44 = new A.SupplementalFont() { Script = "Sora", Typeface = "Nirmala UI" };
            A.SupplementalFont supplementalFont45 = new A.SupplementalFont() { Script = "Tale", Typeface = "Microsoft Tai Le" };
            A.SupplementalFont supplementalFont46 = new A.SupplementalFont() { Script = "Talu", Typeface = "Microsoft New Tai Lue" };
            A.SupplementalFont supplementalFont47 = new A.SupplementalFont() { Script = "Tfng", Typeface = "Ebrima" };

            majorFont1.Append(latinFont14);
            majorFont1.Append(eastAsianFont14);
            majorFont1.Append(complexScriptFont14);
            majorFont1.Append(supplementalFont1);
            majorFont1.Append(supplementalFont2);
            majorFont1.Append(supplementalFont3);
            majorFont1.Append(supplementalFont4);
            majorFont1.Append(supplementalFont5);
            majorFont1.Append(supplementalFont6);
            majorFont1.Append(supplementalFont7);
            majorFont1.Append(supplementalFont8);
            majorFont1.Append(supplementalFont9);
            majorFont1.Append(supplementalFont10);
            majorFont1.Append(supplementalFont11);
            majorFont1.Append(supplementalFont12);
            majorFont1.Append(supplementalFont13);
            majorFont1.Append(supplementalFont14);
            majorFont1.Append(supplementalFont15);
            majorFont1.Append(supplementalFont16);
            majorFont1.Append(supplementalFont17);
            majorFont1.Append(supplementalFont18);
            majorFont1.Append(supplementalFont19);
            majorFont1.Append(supplementalFont20);
            majorFont1.Append(supplementalFont21);
            majorFont1.Append(supplementalFont22);
            majorFont1.Append(supplementalFont23);
            majorFont1.Append(supplementalFont24);
            majorFont1.Append(supplementalFont25);
            majorFont1.Append(supplementalFont26);
            majorFont1.Append(supplementalFont27);
            majorFont1.Append(supplementalFont28);
            majorFont1.Append(supplementalFont29);
            majorFont1.Append(supplementalFont30);
            majorFont1.Append(supplementalFont31);
            majorFont1.Append(supplementalFont32);
            majorFont1.Append(supplementalFont33);
            majorFont1.Append(supplementalFont34);
            majorFont1.Append(supplementalFont35);
            majorFont1.Append(supplementalFont36);
            majorFont1.Append(supplementalFont37);
            majorFont1.Append(supplementalFont38);
            majorFont1.Append(supplementalFont39);
            majorFont1.Append(supplementalFont40);
            majorFont1.Append(supplementalFont41);
            majorFont1.Append(supplementalFont42);
            majorFont1.Append(supplementalFont43);
            majorFont1.Append(supplementalFont44);
            majorFont1.Append(supplementalFont45);
            majorFont1.Append(supplementalFont46);
            majorFont1.Append(supplementalFont47);

            A.MinorFont minorFont1 = new A.MinorFont();
            A.LatinFont latinFont15 = new A.LatinFont() { Typeface = "Calibri", Panose = "020F0502020204030204" };
            A.EastAsianFont eastAsianFont15 = new A.EastAsianFont() { Typeface = "" };
            A.ComplexScriptFont complexScriptFont15 = new A.ComplexScriptFont() { Typeface = "" };
            A.SupplementalFont supplementalFont48 = new A.SupplementalFont() { Script = "Jpan", Typeface = "游ゴシック" };
            A.SupplementalFont supplementalFont49 = new A.SupplementalFont() { Script = "Hang", Typeface = "맑은 고딕" };
            A.SupplementalFont supplementalFont50 = new A.SupplementalFont() { Script = "Hans", Typeface = "等线" };
            A.SupplementalFont supplementalFont51 = new A.SupplementalFont() { Script = "Hant", Typeface = "新細明體" };
            A.SupplementalFont supplementalFont52 = new A.SupplementalFont() { Script = "Arab", Typeface = "Arial" };
            A.SupplementalFont supplementalFont53 = new A.SupplementalFont() { Script = "Hebr", Typeface = "Arial" };
            A.SupplementalFont supplementalFont54 = new A.SupplementalFont() { Script = "Thai", Typeface = "Tahoma" };
            A.SupplementalFont supplementalFont55 = new A.SupplementalFont() { Script = "Ethi", Typeface = "Nyala" };
            A.SupplementalFont supplementalFont56 = new A.SupplementalFont() { Script = "Beng", Typeface = "Vrinda" };
            A.SupplementalFont supplementalFont57 = new A.SupplementalFont() { Script = "Gujr", Typeface = "Shruti" };
            A.SupplementalFont supplementalFont58 = new A.SupplementalFont() { Script = "Khmr", Typeface = "DaunPenh" };
            A.SupplementalFont supplementalFont59 = new A.SupplementalFont() { Script = "Knda", Typeface = "Tunga" };
            A.SupplementalFont supplementalFont60 = new A.SupplementalFont() { Script = "Guru", Typeface = "Raavi" };
            A.SupplementalFont supplementalFont61 = new A.SupplementalFont() { Script = "Cans", Typeface = "Euphemia" };
            A.SupplementalFont supplementalFont62 = new A.SupplementalFont() { Script = "Cher", Typeface = "Plantagenet Cherokee" };
            A.SupplementalFont supplementalFont63 = new A.SupplementalFont() { Script = "Yiii", Typeface = "Microsoft Yi Baiti" };
            A.SupplementalFont supplementalFont64 = new A.SupplementalFont() { Script = "Tibt", Typeface = "Microsoft Himalaya" };
            A.SupplementalFont supplementalFont65 = new A.SupplementalFont() { Script = "Thaa", Typeface = "MV Boli" };
            A.SupplementalFont supplementalFont66 = new A.SupplementalFont() { Script = "Deva", Typeface = "Mangal" };
            A.SupplementalFont supplementalFont67 = new A.SupplementalFont() { Script = "Telu", Typeface = "Gautami" };
            A.SupplementalFont supplementalFont68 = new A.SupplementalFont() { Script = "Taml", Typeface = "Latha" };
            A.SupplementalFont supplementalFont69 = new A.SupplementalFont() { Script = "Syrc", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont70 = new A.SupplementalFont() { Script = "Orya", Typeface = "Kalinga" };
            A.SupplementalFont supplementalFont71 = new A.SupplementalFont() { Script = "Mlym", Typeface = "Kartika" };
            A.SupplementalFont supplementalFont72 = new A.SupplementalFont() { Script = "Laoo", Typeface = "DokChampa" };
            A.SupplementalFont supplementalFont73 = new A.SupplementalFont() { Script = "Sinh", Typeface = "Iskoola Pota" };
            A.SupplementalFont supplementalFont74 = new A.SupplementalFont() { Script = "Mong", Typeface = "Mongolian Baiti" };
            A.SupplementalFont supplementalFont75 = new A.SupplementalFont() { Script = "Viet", Typeface = "Arial" };
            A.SupplementalFont supplementalFont76 = new A.SupplementalFont() { Script = "Uigh", Typeface = "Microsoft Uighur" };
            A.SupplementalFont supplementalFont77 = new A.SupplementalFont() { Script = "Geor", Typeface = "Sylfaen" };
            A.SupplementalFont supplementalFont78 = new A.SupplementalFont() { Script = "Armn", Typeface = "Arial" };
            A.SupplementalFont supplementalFont79 = new A.SupplementalFont() { Script = "Bugi", Typeface = "Leelawadee UI" };
            A.SupplementalFont supplementalFont80 = new A.SupplementalFont() { Script = "Bopo", Typeface = "Microsoft JhengHei" };
            A.SupplementalFont supplementalFont81 = new A.SupplementalFont() { Script = "Java", Typeface = "Javanese Text" };
            A.SupplementalFont supplementalFont82 = new A.SupplementalFont() { Script = "Lisu", Typeface = "Segoe UI" };
            A.SupplementalFont supplementalFont83 = new A.SupplementalFont() { Script = "Mymr", Typeface = "Myanmar Text" };
            A.SupplementalFont supplementalFont84 = new A.SupplementalFont() { Script = "Nkoo", Typeface = "Ebrima" };
            A.SupplementalFont supplementalFont85 = new A.SupplementalFont() { Script = "Olck", Typeface = "Nirmala UI" };
            A.SupplementalFont supplementalFont86 = new A.SupplementalFont() { Script = "Osma", Typeface = "Ebrima" };
            A.SupplementalFont supplementalFont87 = new A.SupplementalFont() { Script = "Phag", Typeface = "Phagspa" };
            A.SupplementalFont supplementalFont88 = new A.SupplementalFont() { Script = "Syrn", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont89 = new A.SupplementalFont() { Script = "Syrj", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont90 = new A.SupplementalFont() { Script = "Syre", Typeface = "Estrangelo Edessa" };
            A.SupplementalFont supplementalFont91 = new A.SupplementalFont() { Script = "Sora", Typeface = "Nirmala UI" };
            A.SupplementalFont supplementalFont92 = new A.SupplementalFont() { Script = "Tale", Typeface = "Microsoft Tai Le" };
            A.SupplementalFont supplementalFont93 = new A.SupplementalFont() { Script = "Talu", Typeface = "Microsoft New Tai Lue" };
            A.SupplementalFont supplementalFont94 = new A.SupplementalFont() { Script = "Tfng", Typeface = "Ebrima" };

            minorFont1.Append(latinFont15);
            minorFont1.Append(eastAsianFont15);
            minorFont1.Append(complexScriptFont15);
            minorFont1.Append(supplementalFont48);
            minorFont1.Append(supplementalFont49);
            minorFont1.Append(supplementalFont50);
            minorFont1.Append(supplementalFont51);
            minorFont1.Append(supplementalFont52);
            minorFont1.Append(supplementalFont53);
            minorFont1.Append(supplementalFont54);
            minorFont1.Append(supplementalFont55);
            minorFont1.Append(supplementalFont56);
            minorFont1.Append(supplementalFont57);
            minorFont1.Append(supplementalFont58);
            minorFont1.Append(supplementalFont59);
            minorFont1.Append(supplementalFont60);
            minorFont1.Append(supplementalFont61);
            minorFont1.Append(supplementalFont62);
            minorFont1.Append(supplementalFont63);
            minorFont1.Append(supplementalFont64);
            minorFont1.Append(supplementalFont65);
            minorFont1.Append(supplementalFont66);
            minorFont1.Append(supplementalFont67);
            minorFont1.Append(supplementalFont68);
            minorFont1.Append(supplementalFont69);
            minorFont1.Append(supplementalFont70);
            minorFont1.Append(supplementalFont71);
            minorFont1.Append(supplementalFont72);
            minorFont1.Append(supplementalFont73);
            minorFont1.Append(supplementalFont74);
            minorFont1.Append(supplementalFont75);
            minorFont1.Append(supplementalFont76);
            minorFont1.Append(supplementalFont77);
            minorFont1.Append(supplementalFont78);
            minorFont1.Append(supplementalFont79);
            minorFont1.Append(supplementalFont80);
            minorFont1.Append(supplementalFont81);
            minorFont1.Append(supplementalFont82);
            minorFont1.Append(supplementalFont83);
            minorFont1.Append(supplementalFont84);
            minorFont1.Append(supplementalFont85);
            minorFont1.Append(supplementalFont86);
            minorFont1.Append(supplementalFont87);
            minorFont1.Append(supplementalFont88);
            minorFont1.Append(supplementalFont89);
            minorFont1.Append(supplementalFont90);
            minorFont1.Append(supplementalFont91);
            minorFont1.Append(supplementalFont92);
            minorFont1.Append(supplementalFont93);
            minorFont1.Append(supplementalFont94);

            fontScheme2.Append(majorFont1);
            fontScheme2.Append(minorFont1);

            A.FormatScheme formatScheme1 = new A.FormatScheme() { Name = "Office" };

            A.FillStyleList fillStyleList1 = new A.FillStyleList();

            A.SolidFill solidFill88 = new A.SolidFill();
            A.SchemeColor schemeColor158 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill88.Append(schemeColor158);

            A.GradientFill gradientFill1 = new A.GradientFill() { RotateWithShape = true };

            A.GradientStopList gradientStopList1 = new A.GradientStopList();

            A.GradientStop gradientStop1 = new A.GradientStop() { Position = 0 };

            A.SchemeColor schemeColor159 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.LuminanceModulation luminanceModulation86 = new A.LuminanceModulation() { Val = 110000 };
            A.SaturationModulation saturationModulation1 = new A.SaturationModulation() { Val = 105000 };
            A.Tint tint1 = new A.Tint() { Val = 67000 };

            schemeColor159.Append(luminanceModulation86);
            schemeColor159.Append(saturationModulation1);
            schemeColor159.Append(tint1);

            gradientStop1.Append(schemeColor159);

            A.GradientStop gradientStop2 = new A.GradientStop() { Position = 50000 };

            A.SchemeColor schemeColor160 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.LuminanceModulation luminanceModulation87 = new A.LuminanceModulation() { Val = 105000 };
            A.SaturationModulation saturationModulation2 = new A.SaturationModulation() { Val = 103000 };
            A.Tint tint2 = new A.Tint() { Val = 73000 };

            schemeColor160.Append(luminanceModulation87);
            schemeColor160.Append(saturationModulation2);
            schemeColor160.Append(tint2);

            gradientStop2.Append(schemeColor160);

            A.GradientStop gradientStop3 = new A.GradientStop() { Position = 100000 };

            A.SchemeColor schemeColor161 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.LuminanceModulation luminanceModulation88 = new A.LuminanceModulation() { Val = 105000 };
            A.SaturationModulation saturationModulation3 = new A.SaturationModulation() { Val = 109000 };
            A.Tint tint3 = new A.Tint() { Val = 81000 };

            schemeColor161.Append(luminanceModulation88);
            schemeColor161.Append(saturationModulation3);
            schemeColor161.Append(tint3);

            gradientStop3.Append(schemeColor161);

            gradientStopList1.Append(gradientStop1);
            gradientStopList1.Append(gradientStop2);
            gradientStopList1.Append(gradientStop3);
            A.LinearGradientFill linearGradientFill1 = new A.LinearGradientFill() { Angle = 5400000, Scaled = false };

            gradientFill1.Append(gradientStopList1);
            gradientFill1.Append(linearGradientFill1);

            A.GradientFill gradientFill2 = new A.GradientFill() { RotateWithShape = true };

            A.GradientStopList gradientStopList2 = new A.GradientStopList();

            A.GradientStop gradientStop4 = new A.GradientStop() { Position = 0 };

            A.SchemeColor schemeColor162 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.SaturationModulation saturationModulation4 = new A.SaturationModulation() { Val = 103000 };
            A.LuminanceModulation luminanceModulation89 = new A.LuminanceModulation() { Val = 102000 };
            A.Tint tint4 = new A.Tint() { Val = 94000 };

            schemeColor162.Append(saturationModulation4);
            schemeColor162.Append(luminanceModulation89);
            schemeColor162.Append(tint4);

            gradientStop4.Append(schemeColor162);

            A.GradientStop gradientStop5 = new A.GradientStop() { Position = 50000 };

            A.SchemeColor schemeColor163 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.SaturationModulation saturationModulation5 = new A.SaturationModulation() { Val = 110000 };
            A.LuminanceModulation luminanceModulation90 = new A.LuminanceModulation() { Val = 100000 };
            A.Shade shade3 = new A.Shade() { Val = 100000 };

            schemeColor163.Append(saturationModulation5);
            schemeColor163.Append(luminanceModulation90);
            schemeColor163.Append(shade3);

            gradientStop5.Append(schemeColor163);

            A.GradientStop gradientStop6 = new A.GradientStop() { Position = 100000 };

            A.SchemeColor schemeColor164 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.LuminanceModulation luminanceModulation91 = new A.LuminanceModulation() { Val = 99000 };
            A.SaturationModulation saturationModulation6 = new A.SaturationModulation() { Val = 120000 };
            A.Shade shade4 = new A.Shade() { Val = 78000 };

            schemeColor164.Append(luminanceModulation91);
            schemeColor164.Append(saturationModulation6);
            schemeColor164.Append(shade4);

            gradientStop6.Append(schemeColor164);

            gradientStopList2.Append(gradientStop4);
            gradientStopList2.Append(gradientStop5);
            gradientStopList2.Append(gradientStop6);
            A.LinearGradientFill linearGradientFill2 = new A.LinearGradientFill() { Angle = 5400000, Scaled = false };

            gradientFill2.Append(gradientStopList2);
            gradientFill2.Append(linearGradientFill2);

            fillStyleList1.Append(solidFill88);
            fillStyleList1.Append(gradientFill1);
            fillStyleList1.Append(gradientFill2);

            A.LineStyleList lineStyleList1 = new A.LineStyleList();

            A.Outline outline78 = new A.Outline() { Width = 6350, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill89 = new A.SolidFill();
            A.SchemeColor schemeColor165 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill89.Append(schemeColor165);
            A.PresetDash presetDash3 = new A.PresetDash() { Val = A.PresetLineDashValues.Solid };
            A.Miter miter1 = new A.Miter() { Limit = 800000 };

            outline78.Append(solidFill89);
            outline78.Append(presetDash3);
            outline78.Append(miter1);

            A.Outline outline79 = new A.Outline() { Width = 12700, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill90 = new A.SolidFill();
            A.SchemeColor schemeColor166 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill90.Append(schemeColor166);
            A.PresetDash presetDash4 = new A.PresetDash() { Val = A.PresetLineDashValues.Solid };
            A.Miter miter2 = new A.Miter() { Limit = 800000 };

            outline79.Append(solidFill90);
            outline79.Append(presetDash4);
            outline79.Append(miter2);

            A.Outline outline80 = new A.Outline() { Width = 19050, CapType = A.LineCapValues.Flat, CompoundLineType = A.CompoundLineValues.Single, Alignment = A.PenAlignmentValues.Center };

            A.SolidFill solidFill91 = new A.SolidFill();
            A.SchemeColor schemeColor167 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill91.Append(schemeColor167);
            A.PresetDash presetDash5 = new A.PresetDash() { Val = A.PresetLineDashValues.Solid };
            A.Miter miter3 = new A.Miter() { Limit = 800000 };

            outline80.Append(solidFill91);
            outline80.Append(presetDash5);
            outline80.Append(miter3);

            lineStyleList1.Append(outline78);
            lineStyleList1.Append(outline79);
            lineStyleList1.Append(outline80);

            A.EffectStyleList effectStyleList1 = new A.EffectStyleList();

            A.EffectStyle effectStyle1 = new A.EffectStyle();
            A.EffectList effectList37 = new A.EffectList();

            effectStyle1.Append(effectList37);

            A.EffectStyle effectStyle2 = new A.EffectStyle();
            A.EffectList effectList38 = new A.EffectList();

            effectStyle2.Append(effectList38);

            A.EffectStyle effectStyle3 = new A.EffectStyle();

            A.EffectList effectList39 = new A.EffectList();

            A.OuterShadow outerShadow1 = new A.OuterShadow() { BlurRadius = 57150L, Distance = 19050L, Direction = 5400000, Alignment = A.RectangleAlignmentValues.Center, RotateWithShape = false };

            A.RgbColorModelHex rgbColorModelHex15 = new A.RgbColorModelHex() { Val = "000000" };
            A.Alpha alpha1 = new A.Alpha() { Val = 63000 };

            rgbColorModelHex15.Append(alpha1);

            outerShadow1.Append(rgbColorModelHex15);

            effectList39.Append(outerShadow1);

            effectStyle3.Append(effectList39);

            effectStyleList1.Append(effectStyle1);
            effectStyleList1.Append(effectStyle2);
            effectStyleList1.Append(effectStyle3);

            A.BackgroundFillStyleList backgroundFillStyleList1 = new A.BackgroundFillStyleList();

            A.SolidFill solidFill92 = new A.SolidFill();
            A.SchemeColor schemeColor168 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };

            solidFill92.Append(schemeColor168);

            A.SolidFill solidFill93 = new A.SolidFill();

            A.SchemeColor schemeColor169 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.Tint tint5 = new A.Tint() { Val = 95000 };
            A.SaturationModulation saturationModulation7 = new A.SaturationModulation() { Val = 170000 };

            schemeColor169.Append(tint5);
            schemeColor169.Append(saturationModulation7);

            solidFill93.Append(schemeColor169);

            A.GradientFill gradientFill3 = new A.GradientFill() { RotateWithShape = true };

            A.GradientStopList gradientStopList3 = new A.GradientStopList();

            A.GradientStop gradientStop7 = new A.GradientStop() { Position = 0 };

            A.SchemeColor schemeColor170 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.Tint tint6 = new A.Tint() { Val = 93000 };
            A.SaturationModulation saturationModulation8 = new A.SaturationModulation() { Val = 150000 };
            A.Shade shade5 = new A.Shade() { Val = 98000 };
            A.LuminanceModulation luminanceModulation92 = new A.LuminanceModulation() { Val = 102000 };

            schemeColor170.Append(tint6);
            schemeColor170.Append(saturationModulation8);
            schemeColor170.Append(shade5);
            schemeColor170.Append(luminanceModulation92);

            gradientStop7.Append(schemeColor170);

            A.GradientStop gradientStop8 = new A.GradientStop() { Position = 50000 };

            A.SchemeColor schemeColor171 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.Tint tint7 = new A.Tint() { Val = 98000 };
            A.SaturationModulation saturationModulation9 = new A.SaturationModulation() { Val = 130000 };
            A.Shade shade6 = new A.Shade() { Val = 90000 };
            A.LuminanceModulation luminanceModulation93 = new A.LuminanceModulation() { Val = 103000 };

            schemeColor171.Append(tint7);
            schemeColor171.Append(saturationModulation9);
            schemeColor171.Append(shade6);
            schemeColor171.Append(luminanceModulation93);

            gradientStop8.Append(schemeColor171);

            A.GradientStop gradientStop9 = new A.GradientStop() { Position = 100000 };

            A.SchemeColor schemeColor172 = new A.SchemeColor() { Val = A.SchemeColorValues.PhColor };
            A.Shade shade7 = new A.Shade() { Val = 63000 };
            A.SaturationModulation saturationModulation10 = new A.SaturationModulation() { Val = 120000 };

            schemeColor172.Append(shade7);
            schemeColor172.Append(saturationModulation10);

            gradientStop9.Append(schemeColor172);

            gradientStopList3.Append(gradientStop7);
            gradientStopList3.Append(gradientStop8);
            gradientStopList3.Append(gradientStop9);
            A.LinearGradientFill linearGradientFill3 = new A.LinearGradientFill() { Angle = 5400000, Scaled = false };

            gradientFill3.Append(gradientStopList3);
            gradientFill3.Append(linearGradientFill3);

            backgroundFillStyleList1.Append(solidFill92);
            backgroundFillStyleList1.Append(solidFill93);
            backgroundFillStyleList1.Append(gradientFill3);

            formatScheme1.Append(fillStyleList1);
            formatScheme1.Append(lineStyleList1);
            formatScheme1.Append(effectStyleList1);
            formatScheme1.Append(backgroundFillStyleList1);

            themeElements1.Append(colorScheme1);
            themeElements1.Append(fontScheme2);
            themeElements1.Append(formatScheme1);
            A.ObjectDefaults objectDefaults1 = new A.ObjectDefaults();
            A.ExtraColorSchemeList extraColorSchemeList1 = new A.ExtraColorSchemeList();

            A.OfficeStyleSheetExtensionList officeStyleSheetExtensionList1 = new A.OfficeStyleSheetExtensionList();

            A.OfficeStyleSheetExtension officeStyleSheetExtension1 = new A.OfficeStyleSheetExtension() { Uri = "{05A4C25C-085E-4340-85A3-A5531E510DB2}" };

            Thm15.ThemeFamily themeFamily1 = new Thm15.ThemeFamily() { Name = "Office Theme", Id = "{62F939B6-93AF-4DB8-9C6B-D6C7DFDC589F}", Vid = "{4A3C46E8-61CC-4603-A589-7422A47A8E4A}" };
            themeFamily1.AddNamespaceDeclaration("thm15", "http://schemas.microsoft.com/office/thememl/2012/main");

            officeStyleSheetExtension1.Append(themeFamily1);

            officeStyleSheetExtensionList1.Append(officeStyleSheetExtension1);

            theme1.Append(themeElements1);
            theme1.Append(objectDefaults1);
            theme1.Append(extraColorSchemeList1);
            theme1.Append(officeStyleSheetExtensionList1);

            themePart1.Theme = theme1;
        }

        // Generates content of worksheetPart4.
        private void GenerateWorksheetPart4Content(WorksheetPart worksheetPart4)
        {
            Worksheet worksheet4 = new Worksheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac" } };
            worksheet4.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            worksheet4.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            worksheet4.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
            //worksheet4.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //worksheet4.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
            //worksheet4.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //worksheet4.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{2956F7F1-BDC8-4DCF-87A4-EEC9F99CC7B0}"));
            SheetDimension sheetDimension4 = new SheetDimension() { Reference = "A1:J8" };

            SheetViews sheetViews4 = new SheetViews();

            SheetView sheetView4 = new SheetView() { WorkbookViewId = (UInt32Value)0U };
            Selection selection2 = new Selection() { ActiveCell = "A13", SequenceOfReferences = new ListValue<StringValue>() { InnerText = "A13" } };

            sheetView4.Append(selection2);

            sheetViews4.Append(sheetView4);
            SheetFormatProperties sheetFormatProperties4 = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

            Columns columns4 = new Columns();
            Column column18 = new Column() { Min = (UInt32Value)1U, Max = (UInt32Value)1U, Width = 26.85546875D, BestFit = true, CustomWidth = true };
            Column column19 = new Column() { Min = (UInt32Value)2U, Max = (UInt32Value)2U, Width = 12.85546875D, BestFit = true, CustomWidth = true };
            Column column20 = new Column() { Min = (UInt32Value)3U, Max = (UInt32Value)3U, Width = 34.42578125D, BestFit = true, CustomWidth = true };
            Column column21 = new Column() { Min = (UInt32Value)4U, Max = (UInt32Value)4U, Width = 21.7109375D, BestFit = true, CustomWidth = true };
            Column column22 = new Column() { Min = (UInt32Value)5U, Max = (UInt32Value)5U, Width = 11.140625D, BestFit = true, CustomWidth = true };
            Column column23 = new Column() { Min = (UInt32Value)6U, Max = (UInt32Value)6U, Width = 15.7109375D, BestFit = true, CustomWidth = true };
            Column column24 = new Column() { Min = (UInt32Value)7U, Max = (UInt32Value)7U, Width = 18D, BestFit = true, CustomWidth = true };
            Column column25 = new Column() { Min = (UInt32Value)8U, Max = (UInt32Value)8U, Width = 16D, BestFit = true, CustomWidth = true };
            Column column26 = new Column() { Min = (UInt32Value)9U, Max = (UInt32Value)9U, Width = 15.5703125D, BestFit = true, CustomWidth = true };
            Column column27 = new Column() { Min = (UInt32Value)10U, Max = (UInt32Value)10U, Width = 35.85546875D, BestFit = true, CustomWidth = true };

            columns4.Append(column18);
            columns4.Append(column19);
            columns4.Append(column20);
            columns4.Append(column21);
            columns4.Append(column22);
            columns4.Append(column23);
            columns4.Append(column24);
            columns4.Append(column25);
            columns4.Append(column26);
            columns4.Append(column27);

            SheetData sheetData4 = new SheetData();

            Row row39 = new Row() { RowIndex = (UInt32Value)1U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell432 = new Cell() { CellReference = "A1", DataType = CellValues.SharedString };
            CellValue cellValue38 = new CellValue();
            cellValue38.Text = "8";

            cell432.Append(cellValue38);

            Cell cell433 = new Cell() { CellReference = "B1", DataType = CellValues.SharedString };
            CellValue cellValue39 = new CellValue();
            cellValue39.Text = "9";

            cell433.Append(cellValue39);

            Cell cell434 = new Cell() { CellReference = "C1", DataType = CellValues.SharedString };
            CellValue cellValue40 = new CellValue();
            cellValue40.Text = "10";

            cell434.Append(cellValue40);

            Cell cell435 = new Cell() { CellReference = "D1", DataType = CellValues.SharedString };
            CellValue cellValue41 = new CellValue();
            cellValue41.Text = "0";

            cell435.Append(cellValue41);

            Cell cell436 = new Cell() { CellReference = "E1", DataType = CellValues.SharedString };
            CellValue cellValue42 = new CellValue();
            cellValue42.Text = "11";

            cell436.Append(cellValue42);

            Cell cell437 = new Cell() { CellReference = "F1", DataType = CellValues.SharedString };
            CellValue cellValue43 = new CellValue();
            cellValue43.Text = "12";

            cell437.Append(cellValue43);

            Cell cell438 = new Cell() { CellReference = "G1", DataType = CellValues.SharedString };
            CellValue cellValue44 = new CellValue();
            cellValue44.Text = "13";

            cell438.Append(cellValue44);

            Cell cell439 = new Cell() { CellReference = "H1", DataType = CellValues.SharedString };
            CellValue cellValue45 = new CellValue();
            cellValue45.Text = "14";

            cell439.Append(cellValue45);

            Cell cell440 = new Cell() { CellReference = "I1", DataType = CellValues.SharedString };
            CellValue cellValue46 = new CellValue();
            cellValue46.Text = "15";

            cell440.Append(cellValue46);

            Cell cell441 = new Cell() { CellReference = "J1", DataType = CellValues.SharedString };
            CellValue cellValue47 = new CellValue();
            cellValue47.Text = "16";

            cell441.Append(cellValue47);

            row39.Append(cell432);
            row39.Append(cell433);
            row39.Append(cell434);
            row39.Append(cell435);
            row39.Append(cell436);
            row39.Append(cell437);
            row39.Append(cell438);
            row39.Append(cell439);
            row39.Append(cell440);
            row39.Append(cell441);

            Row row40 = new Row() { RowIndex = (UInt32Value)2U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell442 = new Cell() { CellReference = "A2", DataType = CellValues.SharedString };
            CellValue cellValue48 = new CellValue();
            cellValue48.Text = "17";

            cell442.Append(cellValue48);

            Cell cell443 = new Cell() { CellReference = "B2", DataType = CellValues.SharedString };
            CellValue cellValue49 = new CellValue();
            cellValue49.Text = "18";

            cell443.Append(cellValue49);

            Cell cell444 = new Cell() { CellReference = "C2", DataType = CellValues.SharedString };
            CellValue cellValue50 = new CellValue();
            cellValue50.Text = "19";

            cell444.Append(cellValue50);

            Cell cell445 = new Cell() { CellReference = "D2", DataType = CellValues.SharedString };
            CellValue cellValue51 = new CellValue();
            cellValue51.Text = "1";

            cell445.Append(cellValue51);

            Cell cell446 = new Cell() { CellReference = "E2", DataType = CellValues.SharedString };
            CellValue cellValue52 = new CellValue();
            cellValue52.Text = "20";

            cell446.Append(cellValue52);

            Cell cell447 = new Cell() { CellReference = "F2", DataType = CellValues.SharedString };
            CellValue cellValue53 = new CellValue();
            cellValue53.Text = "21";

            cell447.Append(cellValue53);

            Cell cell448 = new Cell() { CellReference = "I2", DataType = CellValues.SharedString };
            CellValue cellValue54 = new CellValue();
            cellValue54.Text = "22";

            cell448.Append(cellValue54);

            Cell cell449 = new Cell() { CellReference = "J2", DataType = CellValues.SharedString };
            CellValue cellValue55 = new CellValue();
            cellValue55.Text = "23";

            cell449.Append(cellValue55);

            row40.Append(cell442);
            row40.Append(cell443);
            row40.Append(cell444);
            row40.Append(cell445);
            row40.Append(cell446);
            row40.Append(cell447);
            row40.Append(cell448);
            row40.Append(cell449);

            Row row41 = new Row() { RowIndex = (UInt32Value)3U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell450 = new Cell() { CellReference = "A3", DataType = CellValues.SharedString };
            CellValue cellValue56 = new CellValue();
            cellValue56.Text = "24";

            cell450.Append(cellValue56);

            Cell cell451 = new Cell() { CellReference = "B3", DataType = CellValues.SharedString };
            CellValue cellValue57 = new CellValue();
            cellValue57.Text = "25";

            cell451.Append(cellValue57);

            Cell cell452 = new Cell() { CellReference = "C3", DataType = CellValues.SharedString };
            CellValue cellValue58 = new CellValue();
            cellValue58.Text = "26";

            cell452.Append(cellValue58);

            Cell cell453 = new Cell() { CellReference = "D3", DataType = CellValues.SharedString };
            CellValue cellValue59 = new CellValue();
            cellValue59.Text = "3";

            cell453.Append(cellValue59);

            Cell cell454 = new Cell() { CellReference = "E3", DataType = CellValues.SharedString };
            CellValue cellValue60 = new CellValue();
            cellValue60.Text = "27";

            cell454.Append(cellValue60);

            Cell cell455 = new Cell() { CellReference = "F3", DataType = CellValues.SharedString };
            CellValue cellValue61 = new CellValue();
            cellValue61.Text = "28";

            cell455.Append(cellValue61);

            Cell cell456 = new Cell() { CellReference = "I3", DataType = CellValues.SharedString };
            CellValue cellValue62 = new CellValue();
            cellValue62.Text = "29";

            cell456.Append(cellValue62);

            Cell cell457 = new Cell() { CellReference = "J3", DataType = CellValues.SharedString };
            CellValue cellValue63 = new CellValue();
            cellValue63.Text = "30";

            cell457.Append(cellValue63);

            row41.Append(cell450);
            row41.Append(cell451);
            row41.Append(cell452);
            row41.Append(cell453);
            row41.Append(cell454);
            row41.Append(cell455);
            row41.Append(cell456);
            row41.Append(cell457);

            Row row42 = new Row() { RowIndex = (UInt32Value)4U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell458 = new Cell() { CellReference = "A4", DataType = CellValues.SharedString };
            CellValue cellValue64 = new CellValue();
            cellValue64.Text = "31";

            cell458.Append(cellValue64);

            Cell cell459 = new Cell() { CellReference = "B4", DataType = CellValues.SharedString };
            CellValue cellValue65 = new CellValue();
            cellValue65.Text = "32";

            cell459.Append(cellValue65);

            Cell cell460 = new Cell() { CellReference = "C4", DataType = CellValues.SharedString };
            CellValue cellValue66 = new CellValue();
            cellValue66.Text = "33";

            cell460.Append(cellValue66);

            Cell cell461 = new Cell() { CellReference = "D4", DataType = CellValues.SharedString };
            CellValue cellValue67 = new CellValue();
            cellValue67.Text = "2";

            cell461.Append(cellValue67);

            Cell cell462 = new Cell() { CellReference = "E4", DataType = CellValues.SharedString };
            CellValue cellValue68 = new CellValue();
            cellValue68.Text = "20";

            cell462.Append(cellValue68);

            Cell cell463 = new Cell() { CellReference = "F4", DataType = CellValues.SharedString };
            CellValue cellValue69 = new CellValue();
            cellValue69.Text = "29";

            cell463.Append(cellValue69);

            Cell cell464 = new Cell() { CellReference = "I4", DataType = CellValues.SharedString };
            CellValue cellValue70 = new CellValue();
            cellValue70.Text = "34";

            cell464.Append(cellValue70);

            Cell cell465 = new Cell() { CellReference = "J4", DataType = CellValues.SharedString };
            CellValue cellValue71 = new CellValue();
            cellValue71.Text = "35";

            cell465.Append(cellValue71);

            row42.Append(cell458);
            row42.Append(cell459);
            row42.Append(cell460);
            row42.Append(cell461);
            row42.Append(cell462);
            row42.Append(cell463);
            row42.Append(cell464);
            row42.Append(cell465);

            Row row43 = new Row() { RowIndex = (UInt32Value)5U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell466 = new Cell() { CellReference = "A5", DataType = CellValues.SharedString };
            CellValue cellValue72 = new CellValue();
            cellValue72.Text = "36";

            cell466.Append(cellValue72);

            Cell cell467 = new Cell() { CellReference = "B5", DataType = CellValues.SharedString };
            CellValue cellValue73 = new CellValue();
            cellValue73.Text = "32";

            cell467.Append(cellValue73);

            Cell cell468 = new Cell() { CellReference = "C5", DataType = CellValues.SharedString };
            CellValue cellValue74 = new CellValue();
            cellValue74.Text = "33";

            cell468.Append(cellValue74);

            Cell cell469 = new Cell() { CellReference = "D5", DataType = CellValues.SharedString };
            CellValue cellValue75 = new CellValue();
            cellValue75.Text = "2";

            cell469.Append(cellValue75);

            Cell cell470 = new Cell() { CellReference = "E5", DataType = CellValues.SharedString };
            CellValue cellValue76 = new CellValue();
            cellValue76.Text = "27";

            cell470.Append(cellValue76);

            Cell cell471 = new Cell() { CellReference = "F5", DataType = CellValues.SharedString };
            CellValue cellValue77 = new CellValue();
            cellValue77.Text = "37";

            cell471.Append(cellValue77);

            Cell cell472 = new Cell() { CellReference = "I5", DataType = CellValues.SharedString };
            CellValue cellValue78 = new CellValue();
            cellValue78.Text = "38";

            cell472.Append(cellValue78);

            Cell cell473 = new Cell() { CellReference = "J5", DataType = CellValues.SharedString };
            CellValue cellValue79 = new CellValue();
            cellValue79.Text = "39";

            cell473.Append(cellValue79);

            row43.Append(cell466);
            row43.Append(cell467);
            row43.Append(cell468);
            row43.Append(cell469);
            row43.Append(cell470);
            row43.Append(cell471);
            row43.Append(cell472);
            row43.Append(cell473);

            Row row44 = new Row() { RowIndex = (UInt32Value)6U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell474 = new Cell() { CellReference = "A6", DataType = CellValues.SharedString };
            CellValue cellValue80 = new CellValue();
            cellValue80.Text = "40";

            cell474.Append(cellValue80);

            Cell cell475 = new Cell() { CellReference = "B6", DataType = CellValues.SharedString };
            CellValue cellValue81 = new CellValue();
            cellValue81.Text = "32";

            cell475.Append(cellValue81);

            Cell cell476 = new Cell() { CellReference = "C6", DataType = CellValues.SharedString };
            CellValue cellValue82 = new CellValue();
            cellValue82.Text = "33";

            cell476.Append(cellValue82);

            Cell cell477 = new Cell() { CellReference = "D6", DataType = CellValues.SharedString };
            CellValue cellValue83 = new CellValue();
            cellValue83.Text = "2";

            cell477.Append(cellValue83);

            Cell cell478 = new Cell() { CellReference = "E6", DataType = CellValues.SharedString };
            CellValue cellValue84 = new CellValue();
            cellValue84.Text = "27";

            cell478.Append(cellValue84);

            Cell cell479 = new Cell() { CellReference = "F6", DataType = CellValues.SharedString };
            CellValue cellValue85 = new CellValue();
            cellValue85.Text = "41";

            cell479.Append(cellValue85);

            Cell cell480 = new Cell() { CellReference = "I6", DataType = CellValues.SharedString };
            CellValue cellValue86 = new CellValue();
            cellValue86.Text = "42";

            cell480.Append(cellValue86);

            Cell cell481 = new Cell() { CellReference = "J6", DataType = CellValues.SharedString };
            CellValue cellValue87 = new CellValue();
            cellValue87.Text = "43";

            cell481.Append(cellValue87);

            row44.Append(cell474);
            row44.Append(cell475);
            row44.Append(cell476);
            row44.Append(cell477);
            row44.Append(cell478);
            row44.Append(cell479);
            row44.Append(cell480);
            row44.Append(cell481);

            Row row45 = new Row() { RowIndex = (UInt32Value)7U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell482 = new Cell() { CellReference = "A7", DataType = CellValues.SharedString };
            CellValue cellValue88 = new CellValue();
            cellValue88.Text = "44";

            cell482.Append(cellValue88);

            Cell cell483 = new Cell() { CellReference = "B7", DataType = CellValues.SharedString };
            CellValue cellValue89 = new CellValue();
            cellValue89.Text = "18";

            cell483.Append(cellValue89);

            Cell cell484 = new Cell() { CellReference = "C7", DataType = CellValues.SharedString };
            CellValue cellValue90 = new CellValue();
            cellValue90.Text = "19";

            cell484.Append(cellValue90);

            Cell cell485 = new Cell() { CellReference = "D7", DataType = CellValues.SharedString };
            CellValue cellValue91 = new CellValue();
            cellValue91.Text = "2";

            cell485.Append(cellValue91);

            Cell cell486 = new Cell() { CellReference = "E7", DataType = CellValues.SharedString };
            CellValue cellValue92 = new CellValue();
            cellValue92.Text = "20";

            cell486.Append(cellValue92);

            Cell cell487 = new Cell() { CellReference = "F7", DataType = CellValues.SharedString };
            CellValue cellValue93 = new CellValue();
            cellValue93.Text = "37";

            cell487.Append(cellValue93);

            Cell cell488 = new Cell() { CellReference = "I7", DataType = CellValues.SharedString };
            CellValue cellValue94 = new CellValue();
            cellValue94.Text = "45";

            cell488.Append(cellValue94);

            Cell cell489 = new Cell() { CellReference = "J7", DataType = CellValues.SharedString };
            CellValue cellValue95 = new CellValue();
            cellValue95.Text = "46";

            cell489.Append(cellValue95);

            row45.Append(cell482);
            row45.Append(cell483);
            row45.Append(cell484);
            row45.Append(cell485);
            row45.Append(cell486);
            row45.Append(cell487);
            row45.Append(cell488);
            row45.Append(cell489);

            Row row46 = new Row() { RowIndex = (UInt32Value)8U, Spans = new ListValue<StringValue>() { InnerText = "1:10" }, DyDescent = 0.25D };

            Cell cell490 = new Cell() { CellReference = "A8", DataType = CellValues.SharedString };
            CellValue cellValue96 = new CellValue();
            cellValue96.Text = "47";

            cell490.Append(cellValue96);

            Cell cell491 = new Cell() { CellReference = "B8", DataType = CellValues.SharedString };
            CellValue cellValue97 = new CellValue();
            cellValue97.Text = "18";

            cell491.Append(cellValue97);

            Cell cell492 = new Cell() { CellReference = "C8", DataType = CellValues.SharedString };
            CellValue cellValue98 = new CellValue();
            cellValue98.Text = "19";

            cell492.Append(cellValue98);

            Cell cell493 = new Cell() { CellReference = "D8", DataType = CellValues.SharedString };
            CellValue cellValue99 = new CellValue();
            cellValue99.Text = "3";

            cell493.Append(cellValue99);

            Cell cell494 = new Cell() { CellReference = "E8", DataType = CellValues.SharedString };
            CellValue cellValue100 = new CellValue();
            cellValue100.Text = "48";

            cell494.Append(cellValue100);

            Cell cell495 = new Cell() { CellReference = "F8", DataType = CellValues.SharedString };
            CellValue cellValue101 = new CellValue();
            cellValue101.Text = "37";

            cell495.Append(cellValue101);

            Cell cell496 = new Cell() { CellReference = "G8", DataType = CellValues.SharedString };
            CellValue cellValue102 = new CellValue();
            cellValue102.Text = "29";

            cell496.Append(cellValue102);

            Cell cell497 = new Cell() { CellReference = "I8", DataType = CellValues.SharedString };
            CellValue cellValue103 = new CellValue();
            cellValue103.Text = "29";

            cell497.Append(cellValue103);

            Cell cell498 = new Cell() { CellReference = "J8", DataType = CellValues.SharedString };
            CellValue cellValue104 = new CellValue();
            cellValue104.Text = "53";

            cell498.Append(cellValue104);

            row46.Append(cell490);
            row46.Append(cell491);
            row46.Append(cell492);
            row46.Append(cell493);
            row46.Append(cell494);
            row46.Append(cell495);
            row46.Append(cell496);
            row46.Append(cell497);
            row46.Append(cell498);

            sheetData4.Append(row39);
            sheetData4.Append(row40);
            sheetData4.Append(row41);
            sheetData4.Append(row42);
            sheetData4.Append(row43);
            sheetData4.Append(row44);
            sheetData4.Append(row45);
            sheetData4.Append(row46);
            PageMargins pageMargins6 = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };
            PageSetup pageSetup6 = new PageSetup() { Orientation = OrientationValues.Portrait, Id = "rId1" };

            TableParts tableParts1 = new TableParts() { Count = (UInt32Value)1U };
            TablePart tablePart1 = new TablePart() { Id = "rId2" };

            tableParts1.Append(tablePart1);

            worksheet4.Append(sheetDimension4);
            worksheet4.Append(sheetViews4);
            worksheet4.Append(sheetFormatProperties4);
            worksheet4.Append(columns4);
            worksheet4.Append(sheetData4);
            worksheet4.Append(pageMargins6);
            worksheet4.Append(pageSetup6);
            worksheet4.Append(tableParts1);

            worksheetPart4.Worksheet = worksheet4;
        }

        // Generates content of tableDefinitionPart1.
        private void GenerateTableDefinitionPart1Content(TableDefinitionPart tableDefinitionPart1)
        {
            Table table1 = new Table() { Id = (UInt32Value)4U, Name = "WorkItemsTable", DisplayName = "WorkItemsTable", Reference = "A1:J8", TotalsRowShown = false };
            table1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            //table1.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
            //table1.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
            //table1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{5EFA48B8-3293-4748-AA3B-548E4EF8F199}"));

            AutoFilter autoFilter1 = new AutoFilter() { Reference = "A1:J8" };
            //autoFilter1.SetAttribute(new OpenXmlAttribute("xr", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision", "{EDA87F4E-B12B-41C9-8B20-4850D8E9E195}"));

            TableColumns tableColumns1 = new TableColumns() { Count = (UInt32Value)10U };

            TableColumn tableColumn1 = new TableColumn() { Id = (UInt32Value)1U, Name = "Task" };
            //tableColumn1.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{D2D20073-D29B-4500-9950-65429DC6D749}"));

            TableColumn tableColumn2 = new TableColumn() { Id = (UInt32Value)2U, Name = "Owner" };
            //tableColumn2.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{C927F661-BE31-4994-8586-01344B9F2985}"));

            TableColumn tableColumn3 = new TableColumn() { Id = (UInt32Value)3U, Name = "Email" };
            //tableColumn3.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{59FCC959-4DBF-4AF1-AE6C-E4D7EC0DF48D}"));

            TableColumn tableColumn4 = new TableColumn() { Id = (UInt32Value)4U, Name = "Bucket" };
            //tableColumn4.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{D771E4E6-CF26-433B-9C92-89B5C3B94CD7}"));

            TableColumn tableColumn5 = new TableColumn() { Id = (UInt32Value)5U, Name = "Progress" };
            //tableColumn5.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{13263AE0-78BA-40F6-885A-CAF0CD0A76A9}"));

            TableColumn tableColumn6 = new TableColumn() { Id = (UInt32Value)6U, Name = "Due Date" };
            //tableColumn6.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{1043E455-78E4-49DD-A683-5F4606CC4253}"));

            TableColumn tableColumn7 = new TableColumn() { Id = (UInt32Value)7U, Name = "Completed Date" };
            //tableColumn7.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{C04DACEF-DAD7-4BCA-AF53-92EFBD185868}"));

            TableColumn tableColumn8 = new TableColumn() { Id = (UInt32Value)8U, Name = "Completed By" };
            //tableColumn8.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{92FC41EA-2E85-4564-873E-CC35BF95E44A}"));

            TableColumn tableColumn9 = new TableColumn() { Id = (UInt32Value)9U, Name = "Created Date" };
            //tableColumn9.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{52BCECE3-D415-4F6E-A15E-BA2493618BB9}"));

            TableColumn tableColumn10 = new TableColumn() { Id = (UInt32Value)10U, Name = "Task Id" };
            //tableColumn10.SetAttribute(new OpenXmlAttribute("xr3", "uid", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3", "{8D25F3B9-48FF-46F6-97D7-362D215C02AA}"));

            tableColumns1.Append(tableColumn1);
            tableColumns1.Append(tableColumn2);
            tableColumns1.Append(tableColumn3);
            tableColumns1.Append(tableColumn4);
            tableColumns1.Append(tableColumn5);
            tableColumns1.Append(tableColumn6);
            tableColumns1.Append(tableColumn7);
            tableColumns1.Append(tableColumn8);
            tableColumns1.Append(tableColumn9);
            tableColumns1.Append(tableColumn10);
            TableStyleInfo tableStyleInfo1 = new TableStyleInfo() { Name = "TableStyleMedium2", ShowFirstColumn = false, ShowLastColumn = false, ShowRowStripes = true, ShowColumnStripes = false };

            table1.Append(autoFilter1);
            table1.Append(tableColumns1);
            table1.Append(tableStyleInfo1);

            tableDefinitionPart1.Table = table1;
        }

        // Generates content of spreadsheetPrinterSettingsPart4.
        //private void GenerateSpreadsheetPrinterSettingsPart4Content(SpreadsheetPrinterSettingsPart spreadsheetPrinterSettingsPart4)
        //{
        //    System.IO.Stream data = GetBinaryDataStream(spreadsheetPrinterSettingsPart4Data);
        //    spreadsheetPrinterSettingsPart4.FeedData(data);
        //    data.Close();
        //}

        // Generates content of customFilePropertiesPart1.
        private void GenerateCustomFilePropertiesPart1Content(CustomFilePropertiesPart customFilePropertiesPart1)
        {
            Op.Properties properties1 = new Op.Properties();
            properties1.AddNamespaceDeclaration("vt", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");

            Op.CustomDocumentProperty customDocumentProperty1 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 2, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_Enabled" };
            Vt.VTLPWSTR vTLPWSTR1 = new Vt.VTLPWSTR();
            vTLPWSTR1.Text = "True";

            customDocumentProperty1.Append(vTLPWSTR1);

            Op.CustomDocumentProperty customDocumentProperty2 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 3, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_SiteId" };
            Vt.VTLPWSTR vTLPWSTR2 = new Vt.VTLPWSTR();
            vTLPWSTR2.Text = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            customDocumentProperty2.Append(vTLPWSTR2);

            Op.CustomDocumentProperty customDocumentProperty3 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 4, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_Owner" };
            Vt.VTLPWSTR vTLPWSTR3 = new Vt.VTLPWSTR();
            vTLPWSTR3.Text = "tomjebo@microsoft.com";

            customDocumentProperty3.Append(vTLPWSTR3);

            Op.CustomDocumentProperty customDocumentProperty4 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 5, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_SetDate" };
            Vt.VTLPWSTR vTLPWSTR4 = new Vt.VTLPWSTR();
            vTLPWSTR4.Text = "2019-10-08T19:16:29.6328807Z";

            customDocumentProperty4.Append(vTLPWSTR4);

            Op.CustomDocumentProperty customDocumentProperty5 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 6, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_Name" };
            Vt.VTLPWSTR vTLPWSTR5 = new Vt.VTLPWSTR();
            vTLPWSTR5.Text = "General";

            customDocumentProperty5.Append(vTLPWSTR5);

            Op.CustomDocumentProperty customDocumentProperty6 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 7, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_Application" };
            Vt.VTLPWSTR vTLPWSTR6 = new Vt.VTLPWSTR();
            vTLPWSTR6.Text = "Microsoft Azure Information Protection";

            customDocumentProperty6.Append(vTLPWSTR6);

            Op.CustomDocumentProperty customDocumentProperty7 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 8, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_ActionId" };
            Vt.VTLPWSTR vTLPWSTR7 = new Vt.VTLPWSTR();
            vTLPWSTR7.Text = "74246293-2e24-45c5-a933-ed98cba26873";

            customDocumentProperty7.Append(vTLPWSTR7);

            Op.CustomDocumentProperty customDocumentProperty8 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 9, Name = "MSIP_Label_f42aa342-8706-4288-bd11-ebb85995028c_Extended_MSFT_Method" };
            Vt.VTLPWSTR vTLPWSTR8 = new Vt.VTLPWSTR();
            vTLPWSTR8.Text = "Automatic";

            customDocumentProperty8.Append(vTLPWSTR8);

            Op.CustomDocumentProperty customDocumentProperty9 = new Op.CustomDocumentProperty() { FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}", PropertyId = 10, Name = "Sensitivity" };
            Vt.VTLPWSTR vTLPWSTR9 = new Vt.VTLPWSTR();
            vTLPWSTR9.Text = "General";

            customDocumentProperty9.Append(vTLPWSTR9);

            properties1.Append(customDocumentProperty1);
            properties1.Append(customDocumentProperty2);
            properties1.Append(customDocumentProperty3);
            properties1.Append(customDocumentProperty4);
            properties1.Append(customDocumentProperty5);
            properties1.Append(customDocumentProperty6);
            properties1.Append(customDocumentProperty7);
            properties1.Append(customDocumentProperty8);
            properties1.Append(customDocumentProperty9);

            customFilePropertiesPart1.Properties = properties1;
        }

        // Generates content of extendedFilePropertiesPart1.
        private void GenerateExtendedFilePropertiesPart1Content(ExtendedFilePropertiesPart extendedFilePropertiesPart1)
        {
            Ap.Properties properties2 = new Ap.Properties();
            properties2.AddNamespaceDeclaration("vt", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
            Ap.Application application1 = new Ap.Application();
            application1.Text = "Microsoft Excel";
            Ap.DocumentSecurity documentSecurity1 = new Ap.DocumentSecurity();
            documentSecurity1.Text = "0";
            Ap.ScaleCrop scaleCrop1 = new Ap.ScaleCrop();
            scaleCrop1.Text = "false";

            Ap.HeadingPairs headingPairs1 = new Ap.HeadingPairs();

            Vt.VTVector vTVector1 = new Vt.VTVector() { BaseType = Vt.VectorBaseValues.Variant, Size = (UInt32Value)2U };

            Vt.Variant variant1 = new Vt.Variant();
            Vt.VTLPSTR vTLPSTR1 = new Vt.VTLPSTR();
            vTLPSTR1.Text = "Worksheets";

            variant1.Append(vTLPSTR1);

            Vt.Variant variant2 = new Vt.Variant();
            Vt.VTInt32 vTInt321 = new Vt.VTInt32();
            vTInt321.Text = "4";

            variant2.Append(vTInt321);

            vTVector1.Append(variant1);
            vTVector1.Append(variant2);

            headingPairs1.Append(vTVector1);

            Ap.TitlesOfParts titlesOfParts1 = new Ap.TitlesOfParts();

            Vt.VTVector vTVector2 = new Vt.VTVector() { BaseType = Vt.VectorBaseValues.Lpstr, Size = (UInt32Value)4U };
            Vt.VTLPSTR vTLPSTR2 = new Vt.VTLPSTR();
            vTLPSTR2.Text = "focus_planner_config";
            Vt.VTLPSTR vTLPSTR3 = new Vt.VTLPSTR();
            vTLPSTR3.Text = "Sheet2";
            Vt.VTLPSTR vTLPSTR4 = new Vt.VTLPSTR();
            vTLPSTR4.Text = "Cover";
            Vt.VTLPSTR vTLPSTR5 = new Vt.VTLPSTR();
            vTLPSTR5.Text = "Focus Task Data";

            vTVector2.Append(vTLPSTR2);
            vTVector2.Append(vTLPSTR3);
            vTVector2.Append(vTLPSTR4);
            vTVector2.Append(vTLPSTR5);

            titlesOfParts1.Append(vTVector2);
            Ap.Manager manager1 = new Ap.Manager();
            manager1.Text = "";
            Ap.Company company1 = new Ap.Company();
            company1.Text = "";
            Ap.LinksUpToDate linksUpToDate1 = new Ap.LinksUpToDate();
            linksUpToDate1.Text = "false";
            Ap.SharedDocument sharedDocument1 = new Ap.SharedDocument();
            sharedDocument1.Text = "false";
            Ap.HyperlinkBase hyperlinkBase1 = new Ap.HyperlinkBase();
            hyperlinkBase1.Text = "";
            Ap.HyperlinksChanged hyperlinksChanged1 = new Ap.HyperlinksChanged();
            hyperlinksChanged1.Text = "false";
            Ap.ApplicationVersion applicationVersion1 = new Ap.ApplicationVersion();
            applicationVersion1.Text = "16.0300";

            properties2.Append(application1);
            properties2.Append(documentSecurity1);
            properties2.Append(scaleCrop1);
            properties2.Append(headingPairs1);
            properties2.Append(titlesOfParts1);
            properties2.Append(manager1);
            properties2.Append(company1);
            properties2.Append(linksUpToDate1);
            properties2.Append(sharedDocument1);
            properties2.Append(hyperlinkBase1);
            properties2.Append(hyperlinksChanged1);
            properties2.Append(applicationVersion1);

            extendedFilePropertiesPart1.Properties = properties2;
        }

        private void SetPackageProperties(OpenXmlPackage document)
        {
            document.PackageProperties.Creator = "Tom Jebo";
            document.PackageProperties.Title = "";
            document.PackageProperties.Subject = "";
            document.PackageProperties.Category = "";
            document.PackageProperties.Keywords = "";
            document.PackageProperties.Description = "";
            document.PackageProperties.ContentStatus = "";
            document.PackageProperties.Revision = "";
            document.PackageProperties.Created = System.Xml.XmlConvert.ToDateTime("2019-10-07T19:29:47Z", System.Xml.XmlDateTimeSerializationMode.RoundtripKind);
            document.PackageProperties.Modified = System.Xml.XmlConvert.ToDateTime("2019-10-09T23:35:53Z", System.Xml.XmlDateTimeSerializationMode.RoundtripKind);
            document.PackageProperties.LastModifiedBy = "Tom Jebo";
        }

        //#region Binary Data
        //private string spreadsheetPrinterSettingsPart1Data = "RgBvAHgAaQB0ACAAUgBlAGEAZABlAHIAIABQAEQARgAgAFAAcgBpAG4AdABlAHIAAAAAAAAAAAAAAAAAAAAAAAEEAQTcABQEX/+BBwEAAQDqCm8IZAABAAcAWAICAAEAWAICAAAATABlAHQAdABlAHIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAACAAAAAQAAAAEAAAABAAAAAAAAAAAAAAAAAAAAAAAAAEMAOgBcAFUAcwBlAHIAcwBcAHQAbwBtAGoAZQBiAG8AXABBAHAAcABEAGEAdABhAFwAUgBvAGEAbQBpAG4AZwBcAEYAbwB4AGkAdAAgAFMAbwBmAHQAdwBhAHIAZQBcAEYAbwB4AGkAdAAgAFAARABGACAAQwByAGUAYQB0AG8AcgBcAEYAbwB4AGkAdAAgAFIAZQBhAGQAZQByACAAUABEAEYAIABQAHIAaQBuAHQAZQByAFwAMQA1ADcAMAA2ADYAMwA2ADUAMgBfADEANAA2ADYAMABfAF8AZgBvAHgAaQB0AHQAZQBtAHAALgB4AG0AbAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        private string coverSmallPartData;
        private string imagePart1Data = "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";

        private string coverLargePartData;
U/bunJW7WpRGsWNjrBLrAC/fJpx/z+Hm6x6Y3b7z5mnT1zYSVhfVBDV0F2vq9lFIbVy8OuR66Cvx1db81wMkzraxbOJqV6LqkL4bmDvBDHAB2lAYag0/99biiyHf3NY1dG3zZ59Z5n4CmadPZtASEQUBms3aHh4/cc1ef5X3jfzCTca/wt5DQROKkP5ASRNoY1aTDnmqlnf7ZeuacXo8CWzFjPWwuye8qwBbuH7h+NhHA37SbrSWYbc9IYP/AMPP7tMWb6q94rTyVE6lJXZifL8BhkQhj2Scubf+NEoTQZMZ5rYkGv9Eko6Hnt0qqXDVBwRil7Qi+w/sorORUm0Dp/EClkvhdVSkM+Y48a2Ojmw4ibOZqIfcT77FSkp44NOGwSgXePxJVAJiNgYQmVECfU9Gy/ubgEUvGoVUnJaPlnDAZDfitGD/MpW469JUiAKVRKszKkS38dylxxjqFvw4CwNGjx7zWdS4VLLkb8POX9gas8AFAjb93s7X7h3/4Lbvj4oV9hGxJi7d03NcdGZswMYvPey03y3CGZcCUlBcLlBbC+DclNIsDkKAL4zdfmqrr3fyhru67flUGPQ8I5JJUACaxSgXB5wTbH8Ag9K17PfeLX/zcDfoo94f/Kgk1I86C30HbYtrRQbzG604lcdjrSeIT64va5Een9D+Goz334osvEkCVJkCa0O8I7lI41UqNcBobz7CyJzT7sS/q90djD/sP1xnE3Jd0/CGpFL4EaC2d6PI6ogoRjcvWgQ9eWdd60FqFXLQfF9YbIlrbvvA11iLU4+I6aX+jbS5u/lHb12O8oLax6z6gFxCOc2NiwABATma2CbYrLLUgmvdrW9hG9WsGEuq13OXRBEAoa+gr53kCAJATInCown83TbQ2ANBPwsqx9D51OGI2xC4AMB67NoerbUF4w9DiU9U0EENMlgtfp3XL978NAGgbBn/GJanndWxLi9c8vaJ3d11n+fFoXsZHjxYwxJlLZ6FkG9g9muH2l+sXXDMAaBoqHkCPu2pGrNUawBhcr83Ts+ZG4bnm8fprA/Zix7odAAznut8QLYNmCQFfAml2QLuDBVoyvCXVrvEbgs+wSIYUGwwdcrelZt22O3D37t93//hPH6rovGz8fZTMsnRTHLn+Gmy0uTv45h6ZZZtiTidloE0gen1HABAlvDjuT3/+Z+7WrVtutZFuYWjKIY6aMqVycZRonNcb9+HnH4k2HLRTiq3rpVvR2BvB8Vi53noh5bDoJ0hHImCEYS0UyKYjPT90U2jo9IcMkFcaSMz6lySw1AEuUsl4QlsNx58M4Wx3ndv0mUFdzhRY2g54/72+AljIZiHjms8IaljJ69HJkTCMUCqbdtxk3CMraKDMwOW6I2w/aPFhPHxHLulGZ6WD89nSnQKAXIEdCKdK5tHKAg4FWqR0CGljEytHYNZ1yIzj+uePp+K4QZ8MAEyvyuyxteiDnggALNehXF8MAJYlBDKvMU8wfshUhwAgPof3MU7sUpoJE9cCdsvshyLqOJ5pCRrQ6AFAvX8wAA0AZCm6OoIAAJlkMykC1VKM13ZtP/kBAcAwwWfXWcuQRwBgyBwMTe95AUCXSyBmJcAhABgyJI3hF/pny8v6AAAgAElEQVQ/uMYaQ9pYbfG+qELxVn5m4CACTTDhwPqDuPbzz10nUxQMNiQawCyQ6xCGSQwAco7oszWgEb/jP2ryqSg+Eg2igSmAPSSQYFcuPnOVx4U2JgJrdC2nJpjh7Cz3FY0vvBaqkdhFcyErQVK6A4CJBZvsSMdfsgkNaNWIBc10pDu4Nv9QsMHGkWWNeIWs+YChjSYssKuzZcd9/fVd98HXpywNfHAMFlTXPffyTffcc9dct1dIY5LNCUsIL1+CbAG6eNYBQO+fAeD0UE+zm2Cf/S4AQJzBAtZaszufAFcH+ykBQBnScwBl5wQA5drbj/fHBADtSe0CJeMnel4AkN/TQHoXAPjKK68y4cV+FArIyNdlnw4BQGjqfvjRFzUAkHsiJEnYvKvLffnLr77gel0W0ozr+vPPEhRbreeSUOpBTiArmzRps79BJk0Sxt2CdqWnQEupN6brwefVqqWsIQBItyqtNq8jZzEsBVUGvTUboIel653jkEw4Bj3sk72e63YVGKM0wdYtpqdkEj939bmg07hjkzCU9KeDPd4PSgThlwH0k7JU1SxlV9zA5ySwZV2Ht057F/hu8dgfCTSp5p3JFpSVLlX/dVtI1+bE9XheAK6hD9HryZpAiRwrS7xmvSRgTDNYEtmiycvPq8YxGJIcZ/V34J/JPDXAtS9sS2MAaoLGR9KauLT1aUkhe0a4HgJa2shpEwG2yLSGTMRiq/Gydfl1qDiBb9FVbTzp1mr7GvwaAWTlvioNQLBPqEaz/C1oiOQ/X2VAhmuW40AWJcY9YWO2o+PHoqHLsvsSXJSyZQBzKNWH7rIwLjMnzHIwL8mUNH4A5g988M1SfPEid4eHh+74+ET24a34x5ttn+fZH2Xu5iuvuEFPNIedm7nf//731ASkrdULN80003bcZEr6UQ1Ou78nAQApl+G7Qysg3wAAVtmsckXQELZXyDbFHXB8O8IATTv63KGhmKRuuZHSZ4B1f/7nvyAAiFe+Fn8CGo7yhsx/+B/yjK2Zjg50KsBfv9+V5oiJJPYMAOwNRgQAUUEUAoCZQ+UD1mqUgLZxLu/K/2sXxsHrOycAaHMZTLbQT7XnXEqshDO2/LcRM3YBgDUpt2A/5hpTAlsI1HP8ozi5RsyKAEAbmzp3VK7Z76XfPQAoJcD2spKKJoehyWExxkjlIhvGvAxsqoBD2Qa5+UHZu6XZrzLrwm/FDkcbgPIkAKAdvz4eWgoWaQN6BkWgaRVe466Ma/hZMk5MLLLNtwvLR4PabzuOOdDflgFox2mr9Qe1nRM+Et49+2kGf93J8NQNuU2rprVbb8sVxJRrKwE2pD7Qe8CiLJ+XcYWrGoMhgwlntIVsgbCVVfFvFMOvOjC75sMuEPD7AACbSv5aAUAdZiuJMEczDvhtC/bZGXsOukFhYzIAEEy9vNOl6O8//uETOtjfPDxmBh4MQAJZ/YEAePOHzCQ+OviGGxhKaiQDKEZzbzygRs96PeNmeefN19077/yITgtKYJdbAX5CCjoANDhROO6Dh/cJfIHZhYAWGcXxZOKuXtjnnW+WJxrYiyNmAKDXNC2EObAuhBGwhEbWJneLbYcB+WK4Lw4cu/ICeBSHE6U1YPwB0WDHNyfaWnCEcf+Dbp8Oe6cjgGm/g8zpym03c94bSnpw/dAopSi3zuthv8vPw0FF6Qa6CxNI26yUSSfi3avNkuNzejrl507QFXi5EvYf17zM46XPrIvDtlXR7EybuHRw5f0epwU0HGcnU1kjRSHPS4E5E50miOO150Sj0tZV6EB4vaLcVTQEfSmdaeQgNEBg0xEmGDQ8CGJRhBol0niuGEd19FV0OZzvXIM6XwEYYjwZSgBEChmALBvS8tNUdN7AKLO9sczElo1IPNCiwIzf7yxxY/tIlIEuN2zTACmZCvK3auIqtE38qzFh9EDlvi/fM/tl54mdNw+8KABox8fMaHL0WgHASrAK4A+lsDmvT8TRNeDyTMRqiU4FmAIjWgOwUGzZ5j6B/LDEH6U1BYSzN265mnN+XLm071555aZ79fYN99xzz9GOgBG4Ws/8fUk3RgGLeh1tIqQTBpxQq6bAeVGCbmPDJjz6XNgQCFqa6Gad5240mVDEvDeQgMYYStQUVW0bC7jCnW2+Ek3PjkmpMFhAYCCMZoinh34RulFS+mAr1wU7IQFzdZ1xr0KArFIMFjj6wMTsvjYsKTrCpF7kGZlUH3x1QKmCDTVGJ6oNtmWzADChLuxPJFArjioMQAM2ec8N/ky8X4ZA3bf2QYIBte251HCqStLYWg6fgT1frm9j+LYUNpwbFLOSxvOAhMGziK/Lfv8hAMC2awnfD+1faLe83TGGXNCgic9AxzfL0USjZAAuNs795te/drPFis1mbt1+ndqeKJUwJhTnidoTAwDRFAcA4AcKAPZUbB/rhCX7xlSGzdgW7u7de5zHRTYgA/bZZ68y0XhyciySHFsBOtBdE2sRiSiU2HaVsTXoblhZYIk6u9+SdiHrcbWyEnmrfRXtLUuwdXpIYGpMhHvS/cISrdZUy/wz6wLqmYao/mezE21itXVkBELjGAkKsiPXGzccSDOTfh/sLcd7Abi0yIUJLyBO4RljJgEiJZFIWCgTyRj/qp2dZWV3X6m4qnYtNeDCS3dE++RGKx5Sp03jUrHHYBCKXbMEv1YSKCOf18WSz6kOvZQ++3HJBRy2vcP2GV+abPdB9uHWbdlAReIFSdjIMxn2hsJAtBJuNkaWztV4wZOAH4H5RUBaE1ZbvS+nDDb4pzyOMR0NeAhYf2ZbRGJCG0HofsCOw1rWjP3BbJZP8EQJBPMP8B0DFPEdsPLsO9yjijX3a5emTOxCqw/zxYH9qeNnXabtnHLfqJgA+CSAE4EnSKhkQ/qn3Z4RObQJV+Lc9HRKABD7KE5Jrdm0y/OhmRSAqr2RrId8M3MffPCBy9eyPxrz15hwNh5IYIlNr1YilgCg2n+dv/LZQEJKS4Bx/3J/uhaDMI8VJ96f0/Op/cl0npoWXxw/ghkBfwCl1Kwq0qaFK3QyR8zQ6bif/vSnTOzjtVnN9fqq+ETJcDQguIyrMX6QLMK4CgCYukePZ9Kkqz9kMhS2DNeBknkBbjcyb53aI2/A7Lxqphr0+ONErn1V7Lp+379ZNt2Rt8omP1znOs4WB4fRdVVaKzwLMycyXtG+Uu/dUK1Ys6PYc7b16OO9aN+P42Z/FUoUsVJce78NAPTDa0QVfQOMVpl3UaJIjx/64uF4GFEp+Yu/rAOAHOYAcIqdlhAIDAFAf5ENTkuJiEbirG0MwOh5hQDgLiS5ukDFcFYmWQDyoLumWIDqxIpOXxkPDqTXnJAr88YXJI/g77VzI/Dc4dSFC8RPuBYAMATmmhzm7xsAjMXX4zmw81m1OMrlQvv+AcDwGv2Gr4FsyUDQkta4i3HQxSnO4DDjDCMZgJxPCwDW5oZdT0sJcB3MqwKQ4VRsAv68YTKCX+SAlQZNM1lBCbAYJqPORszDBgCQn08cS3WXLnGffPKpe/e9z9zpyYl7dDwlSNNDyfAmd8Os62azmZt0pWPt4+NHBLyKjTAAe11h3kETD8DdcjWlQ/LijZfdL//8l+LYAuRTrToreSAwlCRuNp8TeDydowT5gM0g4PzNHst59gZ9BgBv3nmFABkYdnhlmmETEePCZUmfAODx6UoYdGnPddEtExoaaced9sYyPxQIQcIY9zOdrggAFhsBylyO0jsw1sSRGw8mPP94ghKE1GvR9TOxY4vFjM0JUIpkjgLGgwAWGBEbNPLANUop3nolGkUYIzgCUy0J5pgSECwICAK8hONlWiAooRD7Z3YUpSgogQZQC/FxKVNwqy3HczGbMYOJcUaJQdh1jvtOpeS0CkyENoFAIYWQpJQRDE9ehzEn1JFAqXgMAPKDcF4VAARrgeUmfMk8xboPGSNW0msAIJ4DHeGtZuC1jBHAalgCjFJhHlUdDXFASwDQA1jBffPzrHoqmQY+AImY7nHpE0rUwvuIGXLheuYY6htPCwDWHNeo/LGeEFCJgycAAGP7xAAu0ITCvLffw6ZlZtMFoK9upBAVBzMPGjcQu37j9Vvuzmuvuf09lCatmFFn1+pcgDQJKkVDi80y/H4gwKQd3+Yx5gnWM9gRfK7QBixsLUkQDcd+7+JFlsyCcYs9A6AFGHweTPQl0iVjgAEhits2uUtyJDTAUNRu1dT/gaaSBvYArSEpoNcPYEMCSjueMQBLJiCXifo2IQDobT4BRCmjzrOhlEAORfvp4VzsyPuffMVxQ6KQoKQTfSJoF+5N9txq8cAzhMSHshIWLbGKmZuRv/BdA4DGBGoDzNoAPAaZPzAAKHa42VH8vgBAW5NtpcJn+RTc771d9JYpnF5V3aywS/sZAOBf/+Y3bjpfMvF385U73OthhRHAGggSlgLTFygcAcD3P/yM5+8rcINQl+wl6+rbQyleTl1g+CnwH1CCeOvWDSYGZ7MTzudlLlIQ20SYgCYBcXl/4Pq9vtsbd6TxljKUaFcI4FkArSWymUmpyLBsXU/2BesGrNfFcl1tgiG2WANn3x5WtO6M0e67fW+EQWgSAEyQgP2GvR52arVy88XCDbqSFO11pfIANgr7+nQl0i2ypQIAlFLLPLdup+b3WSAtEikd9Rs2+aLU/wT4ZYChMfW06VccSPt53pFkCfbBcFysYshK+LB/4z599GlM/MT8HrCzcY1aLptoybftj8GsJFMuFSY2FBQ4xxUI9EwzfY4WoPPZNUkUGRCt++BGu+42AYA4hiVu8JOM1EAX1jTsZKJAYkRsvDHSSxslvg1ASbsu35xC/RqTLhGpDGkIx/0LySVdh5gfqCQRTT4pjQczDUAdAEDZd0tgjPveei1gqAKmRTESwBRQKD8vfqNv6kJ/OnWdnpQ8o5mfXLc2rdqKVA+aWV26dNGN+7JetsWCiajFXLoSY/3xenQ+mb+S9q0C0eJ49dfiuLQhAStzUKQ3Kn4WS3dLYFsWhx1Q4kGbJ75rtLMmdxrvAkfAHNISXTCP0SwQXYAxXks2P5HGR2gCgiYoOC66AMtL/RFdR6XfYziHrEfEo3h+SFSHlT1Hp9IMcDASDWSAsgAKkwQEixIANAagX49W2WXzugEArBj46BdrTuITfWdIW+GcZRPSajMezuuQHBWf9CkAwDDh4glsUfMOO90uABDNOEL8IQYATZbOX37cjPYMALB2y6Hkj2mE/8V/+L854+wiTOQ0zoDGAJmBWE0AII4XarPx9xamFzZIvnaUOoSZ33DA2hwbuYaqkKt/KAEA6EsTy62hMm4hsFeZVBEAaF/y/EYrqQnuu1LK4x219lLNMxmAvotw9fsxU6/NIbTr3VVa6w1lSykO5tEukO+sBd/eRUi+Zcf24rDxwZ6WARhoINl8CU8RlwLVuvyZg2VmN9BwkBuoLvB4vprBaxsj0/Xw8ys2qOYIfksAsK2bdnw9oSFrfN4eCNUMmMYhvgKiBTgtS6BLSjoc6/nasenFR599Ld1zH8+50ecrKaMbaVOJQbKhA/74+KEyAMVxHo4kA9/rAnzpEABEp84XXnzB/epX/4sbD0XjZ5pLUG9ALzZKGN3HJ4/dwcOHtCE472x66g4OD9z2dCGla0XhLl++5H70izd4XjQdwXGyVEtNfGCdul6/7x4eLt39+/fdNO1RQ2fbndCROk7A1stdDi07sOLWiYBsa3TiWrluqt31cnQW7DhkDOmId6T7YNIDk3DNTCwYBeOBaIlZ2Qi6GDKAcdLsgnLCYCRoc4xkI910mVXF5xJ0Al272WzOjC5Kfs2GsLnKGqws69CWUESXDABzWFiaIuLizOhaF8V1QQ3H9cwYS5aYkUDFSo/LeVcyvWXDrTK/BYjZuoLXg2tWpoT6WUjs0vEFC49lSaLNZKXjcMgAXPQycTjhTIUvzzAxShC0aKCjQ0AFDCsBYllypfuMAHtyAVYqbOBT2XxEHEbfJVgdSxsDXC8cFywfal2ppxhq2tl1EpCKtI88Y9wzAEvgHecI7Y0AgFaqXWa6+b6u3yZGn8wHbWKSBb4DGc5Vu21zx7oBx/bTMxCC/ZKMAg0oqfXIkmAB3uJSprBkM0ysWUbdxt+Ox2eNgLXYEjBbnM5Zanv71Zss+X3xhSucLygtA1NjnUuJWcHu1iijFUANzD/MGThwCAhtXG3eoIQZL85NlNnCxugcI7C4kYTCeG/fXbhwkXMCdiTrKaMTcgPo7q0AgX/m3rEWZguAdxFTt/OVCUasawa+CPZYdheUSOu+sfHMvzIYlH03Wm8N3XbZjZAsDxTCq/1GkAIm82BAEHNd9Kjh+vm9BwQSoNmEdTjeuyJSB5kAJniFACDeCX0XS9DE+1ITAFgeq21X3f0+5g+vq6IR2F5i+6cCALbdmdxP/fp3VSDsHqnqJ2IgcCcAqMybKD3oD1oRzm8AANNCNW/JNO+7RV643/zmN+50Kuy1V27eIjMPax4JLNMMl+7bWBuJJAMVAPzwg88lwO0J0AWJIgJM1tyJjbnW9E2QkFwnI+4LP/rxOzzOdov9MnUd/nvL4wLwHvY7tDOv3LgqJcJuJgw7Len088e011LV2FL7Z00qAADypftsbm0oo7YDpTSF7EfGhJN/S+km7RqBFGFc83Z15Clrlqbu5PFjd3x07MbjgTAa1S6IbuLGPTias9tyfyBxnOmfGhOWQI0y8YSZphp6iSTstoV24fV+slbYqH3yXZFzsWMGYBqzEcw5aAtjnMnMRkWHAm34eTo7JUjCagn6Iqj4KNwql2Rn1hGGeVGI/jH2G6wT0YYF202Oh/2HUi/GtFHCCCo35HwGuCqAq34lkr6SCA18Bt0/MU/A0AYbcA0fEAkcLSWl6cHnNjPvZ8ijUWbcVqo6VmtpZkYZE/h6PsEj1WPUv85R4bFR7eac+46MvZTlynNR+RMAcWg4ZU0CfNLLpBos4yZ7caY11L2+aPmdTGfu0dERr8nvadSCBLMU4J+A8EwqQ1Muu6SagjLmuD/sW7YeUDqCfRc+F/7O0nIyz2SeCKN969bLORPknY4AuVmKpNvK+7ui2ZxgQ9bEF/RvC+SlK6/YDpnN9H4MoMTAjhqhw+MEVgIe2VqfmGYJfnlSSONYPCXPQMYXUh22PjGu8POxn/Z6A55/nW9Es3g0cq+//rq7dEG0xZdLlGBL12+OmWmQ1/ZXZcQmOr9ZcYF6IXkdz5bC+B1MagAg/p5BEx2xkTXXCfZr8zObiEzV0a7/ZnbI2yP1X+qfNKalMfSre1s4xuF3/fP1AGDbFZVNe8QwljjLtwEA7Swe71D7Yd14y79XCXKWSPaNS2sAoHzTx+QtOFrbeCRPAgByHKKJ7U8cjWMMAPrSrOhz3wYADA9xFgBIc2kltAFwIpkGi1RM4yGiUOpJnhYAxOYUBib27/K9OgAoC0czEW0lwGcAgLtAv3D8YgCwfJ7V6wqbuVS/X/5m3z2L4hsvt+8DAAy7/FYa1UQLmfMjAABtfofP60kBQLs/HwgHE7QSgOkHdwGAlbnelE35IwOAcVc+e+a+BFi3EGOGeuaPz1SYp6kZbq/JIpkpzEcDAD/++GP34adfkTEGYgubVczm1J/ZLhcEvHoOJXQbNzs95lDB0cYG2IW4Dh1aBO34jGS0bt646X7x579wo4FQ10+NuGiMAg1G0Xn4/v17NKzQ+jk9PXGfffapS1BjBIaAc+7mzRvupVefqwCAKEXgvNJxwPQaj8fu4GhFbaxZKg0nig66EG/dkROtQWypeG1ycexQOotXvtKOfggEADwGzSeYqc/gmOausxWAsK+ixnDWIcYMBweOJRwVsNys5IhNVFA+tIT+H8o2xDEHAIjrYrkyxlsBQFyTMAUlAysleqkXsfXT3OajOkq+1HQtjKB8KUAjS0CQvUYTHi35rcpJiDYL7CfnGK5LQQ5cp7dTaK6ArLKOjwEFLPkF42iNcQEgqqLJHQXgkHPOMtfH7z67W7p/bQAgNIukVEk0/dJEAjXZUwQAFIBP55/uNcyokzEo3zUAECWWdj8SmKmIN4HAswFA+WK1BPi7BgDjUmCzkQYAWhdTXooCgKFPsAsA9HsVn68BwWVAEgKA1rAitIm+nElLRmv2lx2wBRBnAkEbX4ie38Zd2r/kbty86d5++006uv1eTmbxZn2q61GBZZYOI8AU5otPXHQlIEfpsiwgZbAFTj5F9vksYccQ0OQM2PYmE9cfTfRrEpCgm6kcRua/BerezqqdsoCezQ3CdRFs2yKmrgGury2Uf1i3SzAIZf41+z9W7lzuZzqvdc1YSf0KDD+wk3t9jncB3VJocA4vcuwPTmbu4PDQHR4vyTQYDC8QaE27p1GjCdkHDAC0+dEGVMX783dRBix2UB5nRb7sjMqNHwoA5J7ZwrJoSyDHwGq4nr6rf4fMn7OOuT0nAGighR3LB9QRALgstg4MwMenawI/L7/8CjUnWQXAZlhSUtgEACLh9/77n1UAQN8kkGWsKUv0sY+9++67XMfQAMRa/rOf/4w/HZvKFS4bSOmn64hm4DZfMIB/+42X3f7+nuskM+77nXUVgcB6wgv7ihoWWa+KCAAA5D4TlLwaAFWxiypNAUaerO8qc9j8EwBn2GMtYSGJJykJJrC/XJEZubc/JgsIgBXGD2MKSY/7hzM2QxuPq6Wu2B9kXzRGujKOnCRDikJKt3GfIWBnpXnmD+G6aAs9w86aNsg+ayWzBpiQCciEoAI9TjTqPOtepRKsyUiyVQaiahNCEoHjD7kQlFwqQIImKUyQKiALf4LryNdwlyXAtAVI6GpDOx4vIALI/YuBWVui2Fh11HQUTVa+8oX4Fl6qSO23kz1tCQBQwUr8FNkM4b9jzbCDLBJAKOvWMmRct9mN2H5Q+9iXNKsuojbGrMaVcn3wO7mfoWpnuXT3Hzx0s/nMDcdjse+acAMRgOtgm4umtTJy796V5nNkr8LYbgHoamxAXUpHZqGAgrge8d+gMYhEeaINvDYrTQQUC47BQP1/MFnN35KFpCXbajPXEbGllngKkg5MqleSVVJqLutVgbsIAIz37ZjhRQCwUvUi9twIL/i5Xq9cAgYkq1kG2gRkI43sRiP36quvuquX9z0AyGoWxApgYRre7o2DAtQ6n9bKvBS/E4kO0YGcrUQCKeuJBiBsm5UA41AAACk1hKnalBwEI/kJ2X+yTuRCQwBQ7FdMyTQGo91YkOg+Q1bsvACg2Uuf0PojAYBxE5BdDMAwHg/3AV+xsUNizQOANshxCU2tRNqL/hvyWn0wMfDnH0/wsfBhQrS1+RUBUNHzL0tSguLghgkXA4A1B9GaE2gXYDH+cm9VpL+8yqYSYPtrzACM781KDMv3dSLXrj0CAFs08GKDguM3OXjn0QCsLjINZEzbL15/egNtJcC2+EMwrek5/9AAYKalE1YcYMG8lSZagB4zV3w3sogB6DN0erOx4TJmSrkuynl+1qJtMqbckJQBVGfQGoMjBpjjHFcbA7X6tHwAFhkUryWgmihwgLnP6kZYBwCrjq0F0uaUwHVhd1vXcV98/gVFuQFk7e1d4HHRFADU/k8+eJ9OTd+Jk5HnCwbmwwGyi+iWh9IQAFUbdp+Elgoygm+/8aZ75513XLcrGd7TVRn4YgME4w7XcnisAKBLWdqzPFm4jz/52C2PT7khd4vMvfHG6+72G5dVk0S0N8AApA0JAEAw9Y6nqfv000/cPIEe4dotUmEYbDojAejgMKOrH4AJZOJyYQSuFmHpIdx/AyC0k6kG7j0d/8QBYENJBj7XdaP9C+KIZ9C5UwcLWVN1stIVzgfH1hxzOHT4XRxeZKbthetCKbB0dwNQmZYaHAYweKBXAgljgCYbZUIR7JQNnsxB1bih09OQpLESRfxd/i8DGZkPUoKSF1IKZExzY/zhsqjZl0hGHIw/acog2oDGUJSSMLN55RqxUhgAepIZl/Inb7c60s3YHAUD/poYgCZC7vXNgtIRnyBTADAsAT4L1Khp6BrjsmG/iG0Rf1cxcQ/wKKPbfreS0bL7YsnWwnUZAGhObFh6G1oQX5K1I2O3UYYINR4JCGr5kWb6zeEJGZEGDkgCIUpSavACBgDmF1jAAMYACIzGY/ejOy+wKdBz156hg03bpV108TnT4MP3yWjt9gmkY64AeEdAwICPzT1EW5KBrUakFpDZukZzHwTS0Pwbj0cMZNBNk+X5GYTTZT5BcxSBEVi7nPe6Xm1MPWCXggGIwAjBH1gkyoz1QZ+UFKNES5i64uDDbsuaswA82IcCoKviqym7JHyu6GKHa0cTAbzy9Yr2K+tA4iB1882KYEGRQey9cOttRk3Fx/Ole3T4yHXGwmgIHe0SUK+WVjX6DxEoF/ob1YC16dvt71UlcHTO0y9s3i//lADA2FeQwLW81z8GAHje0bZAV7TT2niA4lFUXgp6G9MNdpBBap64//LXf+2OT1Zc4y+9eJMJPDS3EP3alZSsesYxEmE9SlocHhwQAKSEiGndKdMKgTHGsasane+++wdhzm+HPO/Pf/EznmdNyYCCdogSEikkS6D0NOd+8+N/IYzEdPOYXbpHA00g1OIjvV+Nt3yTSWX+ua34KWzc6DVzS+DIMz58pZL6JdF6WS3L/V0SDmXiCoDSw4MDd3x05PYu7bN7sSQVCjcYjdiV9eHBsbt69arbvyCanoVVUqEEFfNuWzbFkuenTGQnzD+zm76kWbscs/2PJgKktNkoUQJ84EV7b6WWOB8ShoWWhCrDDwAornedi38gTVRwTfKzKLTCwcbZ20OdbUa0UEmOVAE7DBVLMe261A8L/WTcX8mkK7WAK/tiJqWypkUHRhsTmmthJPZS695suoEK6BJgRWnsWDWQrYRUASnM105KwNbGivp9lnA0DTq/5nTt6f0WG2kc5QEsj8hU/a9eT6Qnhv0RE8mffvoF/cP9ixeY6AC+GEgAACAASURBVGZXWTb3g4+ujfKgXQefvSjcaH/CdbmaS+f6zVK0cXEMnHsxm/J3NMPCesM2SDAX3XHVuaSeIhhxSPR14LvCGsB/RSWNVbDo/DcpDew5acLmKxyfKL71zFNrAmMJoYiQY4zc3RILeh61Y3FpKPdjZSQbABgCgVtlmrIBYhfMekH2kv7QvQL/5ZlLwgxcPCYTEoA1E5/qh8TSOKbVjlJyzj8fx8o8mK+3bjGfu0536J5/4QU+SzxfVjhR21ib07RUABrOUe5FEZ6jGIvXsNdF4QFAkwLR8a4DgGpNPDBXldravf9oorVGio/i5koFgMkalEePNQDtL3HCMv4dUkEYx10MwNp92Pq0ngU1gK+KJzCp3JC49KQHYwDiRHQ0rXRPA/g2ALDdGWt2kNoyuLEmWnlcexCqrWLEoQhtLqvpg29GgSS3HX3vPAAgPi+d9srZUavxbygB5gO1c6uDYSVONoFtHNoWht2FASumt9BWAtsEANoxuIHavtlSvVJqZTQj7DYGbUBdGwBo17ArC7Cry5+N27cpATYGUWWuRhRZdCGkA6WOS/nMdR77+Rawg5TCTwMclwDrRIm7I/vxCJ4DA8WgfXgbANgG/nFe/4kAgNhQZOzEabD7jwFAAyq9SLWKmhoAiI0epTJJt8+ukr/77+9xw5+M93j8/cnYgZ33j//w9ywh6BWi8Ybz99DtNRPNEmvGUWznDLCxcWED+8k7P3Zvvvkmuw5jI52uFYDX7pyZapAcnzx2d+/dZakBAgQAgADw8hkC6o5LloW7dv2ae+vHz2uXNWSSSwDQxLiRAUcJMADAjz76yK2yMbUAV92xOEbdCZ0dXhCaaiyXLJVbzFcEFjsBBYWlhwzgpUSUAIwyEzInJTUQ6WUJnjbX6E8m4ghkKGUqmcVwmDBOXfX/Mf8BTKIZCAFJYxUBPAiuYYNSGJYcasZTlwVKDI3hxnmZA/zD2EogAACQGeAAABRAr848Cu0urquiC6jailx7cHJZQoFnrs0MFLDEupLSTNXfQ7c6NHKBlgodUJk3AHZoYzQDWtpOuTGzT0j0w3nOwN5TAJABTCZ6cLwe1Vji95QhYPPaGIAMbKzBQRCYe4kMC8CCUs/vEwAEk4TPywjxEQC4iRzDsFzTAMAwg12TDtEBbQIAm+xagWJSA3sBICsjAxltArzBNmXz3+YCTxUl7wB8MWjVeQEgDs8RWe1Xbt50b92+xsAdU9yE7TGfkP3Gy5qQ2DxCQC/+gTrOEQBoDE5zvE0/ideeJG60J9qdHZQZbwtqlbFpgDIDuyryDcCPWp1aumvdkAMvR54bm9pIKTCceJTo21yU9QfGk/Sk5N8NV9AuneiyaJljH+wpQ6QpeReX6ueZsHS9yDiSDwDKtWshAizc38qJFlqRShOkZSG27v7RoQLoCgJaaZ1eQ1NgVbEPqr1n4/I/IgCIe/d+UhvrIuziHPi133UJcDk/z/evbwMAGtDOdegTjs0AIBmAkz1urwD/4ScgmPXM2FwkOgwARAkw1qwBgJZgMa1Ca9Lz3ntgABZuk4xYCvyTn7xDDdH5AlrDWzK7EIjzCjdgDouG8U9/dJsJza6bcf5rDwJqlFXWm/qploj2UjIscQWQ1dx9M04E26O27r9xGEBSs8lpwK/QfZDgUtZhshVaxP3xwE3GY8/26Q+HBAYfAAC8coUMQXnZfqjNtpThaeNdltCqZq9JexTCPDNNNdMUtn2xbOJQxoU8nSp3eH+E2qZIHpUSI7wqBS7QLIR2XROsAIxCP8QY/D7hWEgcyCYcaBZnUgpbASJXPvEpfoxVIJQln7YBNoPbBZJJSPSiJD0HfCobnPkPaWFdbA2MUwCQCVjwAkWTkucGY1GbsPH6VVvamnhgzzImPM+hTbHkXq2yQQk5vgJBOwNb3BRonnOfUcaiJejfffcDXs9wIiWpWdaTed0Rv8uuU8p5ma6WRNTKcQ+0rrV4hgAMUcnBhlm5NKR7fDyTSpIC1TIABHPxYzsp11Wvr+OzgoTNgkAqfTAdfg+8GWNP37f5ZXunf47GYI1wB78vJX3xV1rMXdl8Qz5g3aFjphvlP5mQlf3cmIW+Qg+awKq3C791pZrX6yRzt2/fctevXRVm4Pwx/doCTXpQ8WO4iWfWmQOgJcDQkiSBwoBdKalfbraMQ7q9kbv+/POu083cerV2XW3aAwCQ+7BqkMa33wYAhtV5XJfRBt8GAIZ7XHguVCzxby0IbDup5skBwKZH/LQA4JM2ATGGtPnOZh9L362ciUxwKEge+kwhYzP5938pGoC1B+gprfXuIuFnUcR11mtXiW6dWSVHKx2TZkCxiQEo36x/np/1tdNVTbb42v23W5qCyEbadM9lVkrvQCamd8gUaPBA5NnjZhR6O0YZWJWMSW5IkZZgWGMejmMdQLONJHAew/va0YXOJiACY7tHGZdSI4EZuiBD0AxkNTNA7T7qyP/ZjLU2Q+CfcwQA1rsOVWeELZxYG9M+Vbu+qEZ/N4BZMqLCMwfQszBKiFFYFlcYSOIAVMfPWEzlfDHHQzQNYmByFwOzbW17g2Oi87ZBRvdvDo1fl2aw9cAZ27gzPOU7EHUFwJYn0pXq//3b/4+O1t7ogjRzSJybz+fu93/3dyJaWySihZGJNpdvwlGspMnEBuUFYIityLj5+Z//3F26fIm6NTjeyUI58shd5wJigqFz8nhBoW84jGyKMZ0rI6fLAH5vT0ocxt0j2ZhzdLQFQGeZPqstFobLfNunbtB0I92Lt92xdA8eXFQ7Ic0+0L2QjstKnxeefcCSZkkM35OSCWgRmgg3gQAtQQXDEfeBDZrrUIFCOBR0iADsAVDczHzppTCfFrxfvIQ5pMweAHrWyQ5NENTztiYgxNDocCrTKBegMNMSms1SdAYBBCKwMPtuJYg4n601PG8wv8z20UHUbrB+PZK9VNoCro+A2QhxZTruHdFhQ6k/gFEAgHju/a4wKKjFpkwDXoM6Ygb8YRwF2NXMOkuXMaZxqa+xhMr3edxA5J6ApAFILRoxtt584BN9zne5tX1ENV48k940BdVx9bY5WvgmTg4Nx7Ne1k2xVIdRbTSbV9H+ZscKgcDwmUFDKtQFSwsrYdZA1IBPNYAGxHHfVa0le06co5nMq24PQT40kMShBUOA51WN0O16Ssf20qjvXrvzmnv11ddYire/L0HRermWgBzalRlKx0Xzj0L9ZPRo0xfTT7KyqOVataLQTRhAo9oxJpYAJEqXSZREwe6gNBBOGdYG58fWOmhq6bcHksunwixug7tAvyYBmLElU9fGFaWG6A5KzU+USqmIO5gUp9Dh1HJ/BFyr5YloZCFBoEBAdS1qNUTYrMWXaktZve3rZn/KxETCBIrYdXW4yVwCmi7MF3RfRWB3PDtlwwG0XWZCR7tLx4x5byN0PHJfwtWc6db+BOVgRpq84b2Ge3pN+oP+IO4hyLDz+VU1NOO1VO5/zavMmgT5c7ckar1diOZBXOFy1lrmZ2P/x0+syBDEmr3xge04Oz5ngXerFmDD90MmYOkH2rhH/h9LN1GoK6W4q9XG/df/+l/c4fGcv794AyXAEwfmLeY7SgJhvzM0teA6KAgAFpuem82m7h//+wf8XG8g3W6tJM7sGd6Hnfjyi7vu+PjIbfui3/mrX/4ZKwWgJYfXAh0zO5AzmRCk6CTCcHr7zku0O4POignJjtbomaxEnCC1+Y7mBbwevX0QBGCXoLEpfyiZWaEOIxJOeNnTTTVRaFq1kBgBwCIMTAG5TPMQ9uPew0fUA6bvBemSzYIJk1GvQ03krw+n7srly+7q1cvs+j0cDqQCoQMATtaMabdaoiKcSqbJyFvQRJytSSbKfPOjqj3E/cj+KgxpYaqLxikBLSZG4NOpX+H37+r8sf2+7BYcdiW2pk9SnWBkDH+t9BekWkLiHlyVjrSXZVCfvWa/1S5aws26Fev6N2ADQGAYY4RxEd/XLggohSarTrUD/bwJmvaFxzG71DWGq80vX4mhGr8AJoPKi1ASQcZBJXFU8/Lu/W/Y7A1gnDSpALtWGq7RriMlRWahav510MUXEj3KDtR4skwCAqwVbcfT6an75sEjJvTSVLpSc89Q6ZrLly65l15+nn44gL/33nvfbZeQ81gTuMVeaNrN1rU3mFUVC2fr3WsUmw8e4Aj4AiqV1FDUTC8T3+roGvAHO8D16KtNVILM/Dj10yyxt/WImCTIcN84LrSIWaUzQlPBnvvZj97g+Ha2Qhwg51I1ADGWpRZg1Y7Cr6O/bfOO4PjGzVfOLVHun/R9F2ASEtK1aDEqoB4yhpv2nhjwiz+DEujqqzm+DxPU8TFYJqvSBW3urL9ra0pjjM54P1X/1LZhiUWq1xTiP7mWoMf+mY9vIDmBedDgv2E9oulPCM41jSHtYVQabZ8Tm9rgNPiycpFrCk//PxwAKINVboGhLpxR6PkJzZRzwFtSo75GuwYC7gYAqyVuZwOAvqumbozfFwDoSzXj+2kQk2xiBKC0zjbEJgAwZAj+cwQAbaHZhuy7duofvm8A0GdMA50nnNq0YgCUha82ANA26u8LACyvoSqS2wYAegAZACACXF1va4emDWCKdAmQffDhp9Sg6bguA3MHZ2GzcV98+BGBMgBLeAbYWMnQQkYSTswGJRRwvMV4oxrh4qVL7ue//DN38eJFBrwsASikFAGAjtHgyQw8XbkHD+672XSlDBXZ6PE3MsdQQgyNlfweHSRoERKI1POj5BivrDvixj0reu6D9z9whzNhpPXGV/j347V04bV5tM4h5Lt0yxm69K4FxFMbwK69qkVjjnNnIxp31umXgBUCnCzlda4U1MS8xXkFyOgQGMTvw60wgHBsnA/lzQaESoZPNkAA620AoGxAklgpEw2pZKb1e3hO1BBBYzMAi+qY+nVV05ctM/2mvWNzjIBmBAACMJPSbykdQbMUAYD0+aP7aCdzA3T8xf/avMFnz63ERwMhKf1FcxAZpwpTIwIAKwFXoAFIoNXYVFriFjIAw3UbOwhh06xwg28DAMGkx+ewT7D0ShMxTwsAmiOK81ZKpGu6L1UnqankkIFacBzuuQ0AYBjIGRideKaoAuzGbN8uaC+Szoj2AFAAgg8w3aSEVjSERv3Evfzyy+6NV264a9evs+Muxqvfl7W3Wa0qXfAw/h5sAnOCzWJk3eBl94f+otbdED9tXoM5idIolB5iDY/39gRMpA6gld2aNpQ1JAjOafuLlvcx4RPpHBLAJuCJ6xOReWMmAuA3Rw/rAjZKSvpFswn/w45OTw89A4h+D0DzoOzDdORCp9G0Gvn8WOYX+DLaLMcDaApEhwAgxw8aVjiPAuKrYkPGwcl8RmAEJXG8Fwi+q5MrWl4KhBiI6rt3VwFAz5oN/DiuhSbAqaHk+TwAoOzD1kWyucnG/wQAS2mFiqNiv7QAiL6sX6dWCSA2A4CdbVd1qgr3N3/zN+7+wWOuuxdfuiEM30ya5RgA2OnIfhYDgL///fvctzrothkwXCyB1u33CYjdu/tA2Ppb0RJ+5+3X3YWLF12vJ/vGUkvhgQ3BZxgOpHTvlRcuEwAEA1DWj1VO6IBYabICK/CBJIAs4xdZo9Jsa6NMQAMYamMclLDK+hbAxEpxvTadJV5oK8pEMUoBIaHiGc/5wg0GQ9dPtwT8Hi8d/anJZMT1i27qbNrQiYkBqhVmDCeVeDCA3uyTJTjNtnSgSacJcL/vo1rDM1uEOej3G6vcUsBmrYxoL02hJbwGMO0imth40rc0mQMkYTUZak2aoE1HPw3VqKpxLN13taTaz2PVWrOST9No1EoHI0yYX4R9L7Svlpi0fd3vn1rxYQCgnwfBPhbODTsPABjOc30udlyLP02CRRLOkvg0f1R8MGj6oZN9Rrt9DxqA0ynBcNtHrNM9uydrHIPKIANqzUfCcVGBQs1usGbhb2c9ZcYJAPjg/qEHAJG8RXUP10MvdZcvX3bPoBRWtXOhub0+PRatTehyErjShBaThSWhIkzs8H3dA/1+E+AHMo6yHjcWfwV2LPTXMG5cd6xIgbatET5sv9J1HTSvwee9RiXUCgDsKpiOLsn088hgzd26n7nBcNAIAJLFqwBbGwDoK7Ws8lMZrmjCCPsGAPD556+zKotxgQMIiItSuxVIBjTZ93+OAKBfyLTP3w4ALP33qr8aj9E6KnVuGkOZPzrrdvHG7ACG50SEGx4rrAZoYwAa8lwDOOwEkbZEfOFtpUD1G2zGbD1VV9u3tw1MnUEYOQi1Ek5pF21GrIaeYgM5Q+slLrkVoy/MNw5uhMaG43ceANA/nMCgWEAkx68yAA048ePVolFTd3w1I29abcGYyI1EmTK9LzojwT2a4+EDTaXMN5XANpYDq3ZH/HxLSvbZjL+medHo6Ld0xzEGoB0nno0iiIzAOsoCtGXqn5gBWD1urSmIBb4RAGjTuqYBph0c25i3TwoANmUYKhlJZQDW13/JPAz/5jVigjeN0Ya34FBgo95mPQI6J6dr99vf/dbNHi/4HPLFmo7+8YMH7vj42GcPIUZMRyQF82zjtmD+kam2ocN64eLYXbp40b3+1utSgtMTptCqwHmQJpaMmd0vSnCPjh65hw+PCBZ2sz4BSJv7cHTZXXZ1wDvJkqWAcD6jL9ojRSEahetk4r788kv31TcCDK7diI5MMZKSODoHdJASMhxnc3QhRfmrAIbIqDPjHTHU4NjwOjJkVCGaLF0LwZijA6qaL2BAwIkFM8qceVxHR5sd4BrYpdNKASyRbY6rMgNiyUswyITxo2LTNsG8loyU/q4XYGIWUGzk/fvZ0dplsw4A8nkGKWg6anoeOMqclwqYZIEUA8YNpf4AAPt4RgBM9TixJIMxIcD4w/cAANLBVECN0FoA7EmpTmn7jYFp3QpD5xbXzv56EZATrg/b6M8LAKJkJtT4MiagASDmSBpA67dvZRDHGiT299hOELhjoFVaSFkLEfAXVQTADoX7BZ3+IKNqjG2f4dXSLWEKYi1rqZhPUFUdKgBfAPiwL1J0vDNgILJaIVAduOX8mMy72zeed6+99pq79uxlDXhyNhHaFhAmF/YtEwmQW9cSd9gTrm0kCzzQpNqVVrqmGisWnBq4RoA+TdzVK1fpoGP9cR1o4OrLxjelL+LHvqazqjIoupa5frScq4CeJda9sv3E5gjL0sqOcV/G+sP3EDjQ9lD2YE1b45mWxnC0rpDBfh8mAKu+TINUii8ZK0ul6StpF1NLFCBwFnsv82q+WtL+HS+kRLILOQgFtgkg67oG2An7Vuj4UasxLG21f7fs+6E/VPMBA4DXr03PALR3jNljoOQOKm3NUdHv2zM17dTzMgDN/1YGSpvUin9mbf7P98QA3Mn885O9fdxiJqDYjSqDxaRboOpKBuBy6/72b//WffbVPa7/a8+/5K5cvoJ6R88QR1BqACAADSSEEtdnqevvfvcBWeFAsARY74qURardvwdDAoDHxycEOLaDfTKeRgNhO/X7oi3c6QrzH1qYBOrXS4Iaw45InGTF3I0nY9dlF03MIV3j0f1hX2ciypfSakKK8z9zg/FFqQhQYMHGxwNbYMdQugL2ERppKMvEvckDWOUmXVLOY9w3Qn2M98OjGccN4yD7NhKOXddHYrDI3TfHBywNBmBKW6k/rdzTbFCRCmAg9r9k9EOyhMcFAKIlwAL46fk8gAkWGehtG5/oM4DW72kEbCU+8sz1msKI+h/q18yty64CXH5aqt+N+cX9H7kfk32wZlLsNgtGW+6SQjULEwGsTEJlq11v6V9qkwucw0y8adCFdkY05jWxnWkps5cGsWZTcgObLeZZyoSJjUfoc9j42zIHIcNKVjnOtQoiOa5Ilsgx4YdvWYGhey/8mKBC0Own9jd0fMdeMpns0x/GOYz5Z3ss5wZY9ixprZayY60JI1AZrT6eyrjOvnlwwOOnqTDfF3NhD3b7mZSiX0BXbhTUF2ziNz8SwDAGAAHom5Y//WoFekK/J0zshont0JTn1pVb3wzjc64HXWe+K7dpAMZ2N2ieJ/NXnkOqPohZvRoAmEmn55/99G3uo1mChn5YvfKNEgCUCywTBepfq39mADkkAzGukD9m1+Zthz4Mkij8/lYTCAQASy3spj2U19/QICQcPxACzvMqGYCRv2njZNrLLQmlkgEoZ7Pn3Mbcq/ESvc8qR/LEqVTsVFyxGMfxlsDluQP/DgzA87xaAUAPBOhxtJle6EaE8YGdy8+nfw4AYDxo1QE7m0JqXfW8hgYz9mcDgHygOxiA4TWEACC/25BR5qSpaBM2P/h/7gCgOdZPAgA2Gooo4DvPIrHPfNcAIJ+dNnfw12Gldi1M0Ph6mzQMeZ0tJcn2/UQzwXU7oUs4adaCCUv2KnM1CjB22Z/WBIAeNA0QodCwlUBBDBDUNd9CABDNHJhhyvpa2tF1/+3v/pt7cO+hlOgVjqLy86Mjd3j4yGcL02ItQXACLTs0DkD2KnWDfodsn0tX9tzFi5fcSzekoxUAQKzT5UaclHC9A0hbLtfuEF0rDx7zTqFpZcE1fh+O+m48GrtsfSh/d+jWBgaOAHwGBGa9Cb+3cRN3+OjQvfvxIzpSEA/H+1N1yOicw/Fe5+50OvV6ZGgKgI0dgKMx12hjzCFEth0BRLolAMjyRbJjdKMOGEtwyBIwB6wcGOBhLs1LMGYIvFm+Q6aTlZ6UGbBQpNie9drvTBroK7Rnosb5Oqfo/3opWiwGAJJhpcB6mXUOkwslAMjAHyLelYytzWoF35S67ysmdF4isML3M6cl0wBM2GRBu8nVSgAkQ2Ylv2BwhQBgRduPJUpWGih2/4cAADkS/jlXGbhWgmmasqVdkX8VLU24dgGA5R5Xrm8Gd4F9MSZJ6Eibpp231VZ65O2pBDxrfX4hAMh9W+2lMSEAFOJ5ohSMgVinR0B9tRYA8MrlsXvttTvuzq0X6cQW+ZIMBTA6EYDna3SjLPgdBisatCAnAJCp2+0xkEFgKYwhAQCFQQGrLvo5ANgs6EPAM9kb005x7iDRsJESY5SsInAnKACdP2pYqj8SJ+ECX8KCMSsV5D2D8dcTplLoc7DxiHbPZvIgTQR8ZEdslEICAFxxno9GXXbzRBkuA0KAGkEpsJ8v0TnCPaVk20l4UdnXVKtKGKqpBwDLrp3K5u0kLGWC5ABs0NF8ym7M06VcJxALSh2o5hCD2GLrMmUQxgCgtw5tIneWOGgqn2kBAO3Z68qRZaddvZukbJqSZ+W4fXcAYBv4J2bBap2aI62yaV/kRz9lCfB3AQByvI2J45s01QFAAvdbAQA3m8T9/d//vXv3w0+4/p959nn3zDPPkAEoGqJagpeKZATun5qV6cgDgAAgKHKh2rlcSyZ+35dmNpAEwX57ukkJoKPyABIjaIpABlh34AEf7Mm2b3S162yyOeX1ZSpVYXaNUz1MrDAxCZhGgS3V7BPQInGLjTKaYgBQJ5olrjJlzya+WZuM42a78ox8aZIh+we6KcM+GcBhiaVtjoTJlgk1Mnh70oV2uSlEe1g1nVHyaaWf3GecSa3ohXmpFEmUgklLO6aMWiutR4KLAKQC/Hb9kJ4yu8Cx0PtDhQNLo9nIDGNnJfpSIguARhj9cp+jC+KHkdSkYyrsQNkP7KckiMs1YmsbAA/HLZfjWjMTs+llV085nuXxOdQY46hk18cDAAC1GzPP5QEOnf+2V6QT2ndMAwv04/1W7JRaLV9iLL/DfoUv74tBzUWbMxH4U41ssYHlJp9AAxZ/gxxVJyUjDxrX6FYLoI8+e4aOvRv+Gy8AVoORjDtKWbHeADbxegKdQlbkGDOx6LjpbOoePnxEf1ISmgVauZGh2ul13HPPPef29uW40MxFV++Tg2/kGpgoL6uljOFtlTRnAYA8XlhBGMTxZwGAnNfqH8cAoAfqjdATAYDeWpOQVNKOAADiJRU5G7fpphzPn/7kTfH/oaqIpCQlUwqX6XFhZ/jsogSD+Y1WmWFx6mojpdrLVUG/CTaUmsSbhQLIKAXOPPAVzom4qrIywaJfTCv5rM/QfkSl8fHnLR6K57N97ocGAOPqSYufnhYA9BUSah+sYpA1WcE6jQlBHgD83/7j/9WIRNkD3QkAtDDOzs8AbH7052UAGsKNDbrpFZdyYCHTYVLDgo0RL9+8w0qBI2AmpoqbCbQNzwx3jISH4xcCJJYpMG258NqrmoXiULeVAFs3n9jP9dcXMxIjKrNdX1gWLZaiPp67SoDFMsl4tjbBaBOpDiYsAaHarDxnqkBLEdsNSosjHAMBegD/zEyUOQhwCQjV7qc5ox0CgJU1VQMAqybLAEB7N2YsWgmVv18d/90AoAEn8UhVx6e+/qt/LwFAfe4R0G1Hxzxvuu8txeshIi+OuW86oNoK2+3Affrpp+7rLx64e3fvue166/r9nlvPFiryuxAdNwO8tPtvmqA0oesuXRjSqdi/MOQm9vxLz7vJZEKXFI5BkuDfMt/t+hBIzOcrlh4fHhzz+yhFoCOj58FnxuOJ6yeH4sAq8y9LNGDVZihgFhDAyi646fTU/e4PXxOAAPMQTMTZtiuOHrL0GpxDewPrCNcvpb1wVrXrnZWeEBDbug46HWsJBwIZER4XvUjpKqqOt4J+LA20Mjp2K13QMeZmv1y5LXL/1OgSx84Yd+a4lt3KtOscO+kVvvQ0sfOryPZysXaLxdzlyEqiC68CTuW6Ksuf+RgUaAhBQWE0VueP3YP/aQkwDfASdSjN0ero3wHsSUClzD4Vv7byQg/w6TJmeSmfr12bafyVjEyCMpZx1UAivBe7RmamrUTJAwcy/cxq7GIAerBI7U4ooiwBgK7DQJOzuoYj+xQwykNLEGqu2P5jpVbxHufXuAFZAcMvdjx4LARngR01zTgbCSlVVXugTAA0W6ok0JTBiNJRAuOdAUEtAbrWZOQ8d+2ab2q97gAAIABJREFU+9Uv3mGXyskYawF2Yi2MhlwDOqiPo/mOAoBY16JbJJ3xEKQLW09L2vEvrVuz8nFqERU5AYjReEDGMZoFYP0BxGIgCbCBnURNU3NDIK63NcZKlQlo48N5KaiAZNx17ZqGJ3W5/Lgri4ialqZVKdqFYOBaOTVLbNG4A2Cb2xD8ExYlAmHcp4jRh+Lgdu7w2YcAl+gdlQCgaXFZGRgABNoVDUgQqIfNHNg1FQxTrbVPyFZK3ZFe28lCGJoZAstuRn8NgKzpn9b2+x0axn4/Nf/IAEHvD7b4CQ3bO8fN+w+6/vSZxGulxvA3isi3ZAA2lTTbftbkr8Xj9H0BgE3+Y5NP5pkTZg8iQMIAwLj7uw+gtXs95hXWH7pC/OEPf3D/8Nt/4trdv3LFXb9+nU13KEGhCSGsD+6TTqVDOmNqAP72H94XBlLheDzMW2puAVTHGh8KwIBmBCyJ608IOGXdLQNlaA3DHoAJB+Cv20GXVjQzEIbbhYkAlYPUADPrYmEafpr4sm66QfdvAb2FmWdNLra5aBVaHGA/Q5JDaZ8FSCOgyWZaW5f2JQEo4JR0yZXKCCnlTbpj2gZjLmN5CoAmAHyvJ91WUWoLINDWN/Yh2lndeLfWPEL9I5t3PWP0GpOqGKjkCJpbgGFnCQn1cxI0gIDtlt8tcevjPI3rpEQSfoMx/oylr4CgAqrJdl1J3BnzDsxoS2TAdvOcKilS+h7oHKXdeFNhegKANu1D2inY2oD13nGaeLTS5r4hM8qusyRnwDAXX0IZYSoFUff35e/efrKJB4DDqsGCFAOvn35Qx6Gpm/gf6l/p9wy4ApDGRBP3Lvku54oyzFeJaCPifa6/YitSHGTvb3zZNBn98OHSVPbJ4Zj79mw59408cB0mbYRhgIQGSt35HLboNrx20+lCgEQjCCZd2bsyRy3KwVCkgmBnAcwf3v2qAgDa9XsA0IBo85t0/Le6TzfZ0GrMp6Xfgfa9PC9rDFeOL98PgHomLlLT8DRwUp6X7T7Yh8k09iXJZQkwAfoMOqMCAOJ5CgCY83nilVnFjfePo31N/cRYmmW9QSXExk1nuRsNh2wEQob1WvwZNlMKqiLMnu5i/J0lddO0P9hIlABg/VO4Hp/4r+EG1fGMtQTbGID+LEF8Hlau0L+h5nnJAAz97EpzTyY26v4E48JYVDO6PUsqtzEA0641OxL/qVI51MC+jK8i+VMHAOMSo9DppMHwIujNDlv8LjYmMyj42VVnPgQA+QwsYtQHch4AsCkw+jYAoJyyLCv7IQHAXU5kbWI/AQAYP8tw7j8NABgeZxeAZZ+tGQL9QxMAGD7nJwUA4+sB4BK+yutQhkBkOGIAUJo6BJv89wgAyrWXK4qBT9QlNLa/PjiNAMD/n703bZIkSa7ELMLjzKPuvqfPaczRg54DM4PBLLkfln+TOJZc7kKWv4DkQrgQgQgoQgohwBwYHAP0fVR315GVmZFxuQflPdVnZm7hnlnVU42BkAyRkqyMjPDD3ExN9enTp7G7nZekoXQXLwCADJjFCAqzcHGxDPfunoR/+Id/CCf3Tgy4g7A2vrtdWsne+sKYfCxRqcj8QyB/MDPn5dbta+HOM8+EG7euh0N0sxuYozIcHvO8GEKVHUwnAABX7IB38vCsBQDq/nFsAHiDzad0fkYVmHPDMBkio45yWDiQYCcdkFm3CtDQaMIHny7CxWIRTs5rZkjX1ZzgGwIMzatcVJrsI5bOmcOOdm0qXaCVcAbg1tt7ylHD31AOzGcEmxe1cSxQNzbfLgw2pkWE/8PhUkbUSkVcINhLbe35WxdiPiNPTACAhYNnzRdsQ8T1YPOCKDu7CoOdBQagHC1lsJ0ZkNuCFkDpAcBVAGAugcBj8V7g1HoJj4C+WAtj8w0lWHJw4dBYt184b/Y8yGrIAEC9j+ZLfN8DSe210ESNIB+TSQm0KQFAzjtJK7gReFoAYCy9LZoeiAkYxzhCj23Po3SU5PCUJb36luxaKfafA32cL85Mi5pMYpbFpiIGKGM9cB4VAKDOE7vHj70E2JkjWEtwVF9/9aXw3e99L7z+8jN2PDYB2bIZDMd9h9LeyhmAu7BEQ566ISsQDi/mOQBDlS/BoeecdyDaOitaaS3tzNEhEwvTqZUMQgOMgIMzYawBjJX4S/OTjv2Fzc+k7egAgDv8UbeoBwAUGKn1Y2xIY7hoftHRXFtZlZ6BtP3OLk5pT3E/enG9dpSi5gBk3DeLstsuR10SAbweD/RzAFCMSgB64F7RZk2g1Tmig4zrO1tad3RopPLlQH4fGJ0YVZfX1MbvA5DP1qo1FejwKYtS4Lh+/5UBgNKeaq/q/d9+2wAgAAMG5T0AYLzivYS0+0eutVvXwzCfoRxyHN59973wl//H/0UA8OjmTXb8BrM0BwBZkkWtVJv3k8lxCwDEPMP+jlJSJOBwNszPiTe5gBYZShLvn63D0dFh+NrLz4fXXnvdmw6tAzTOCKZvbf8+PjykXXrxuRvh9u1bYTqouS/GplAlM0dMZ3ZyFBvN9l/OObfrg51333WAbZcx7bjuqa+cOgyDg8v93u97ub3XYnSxyQD8qoElLj+7fxoePnxIABDJRexv8J8OptaE4cMP/5F2dTY1reNRNbXEAUuId2HbbG0/jBQkS7iIqSIAUN15QzMzG6XKDDUBQQk2ATgDAAV0qrsw9gsD/DRjPPCvLd4bNkb8YAIzkw6BBESbcWld6BMQWMcuu7RfbFLhYB1bxw9NTgFSHKjUGBsgq0ZPprWY7Mh4CEY5gFKzN9uBdZuPgLmYdqpgyLS8uPcVUkC5hAbns0sKmFQMGK8pNuA1ub8Cf9DmtYgAAHLVBRp+tNlhAIHas7sqMNaDxrRtHSDFWMIvJ4i8MXYnr9v3CewPxrw1vwrrHox4JZ7Qvdb2KNNStHmOzyeNRfqCW29aE6xZIJrJYb1j/mPPhD+G5lYP734aAUD8bnPP4gv4Y7US3oVp7KrY6ral3QBgaobZjuMQn+RxFNabXZQSAW0CjRJxSgCq7TWmFPbsZmQawz/4wVsmVTSAjrclOqgBqO2yBwDE+Mv/yOepAMDFhUkMQT+ZxIjlqYHBBODT3PptAYDyB38bAKDZUU8oZM123AC4m+I64F8RACgJSiV88ERyopnZrEyipZjEgz/6j3/ayX2KjtFeiWP7CF2aeDSAqjzoKbG4yjEpAbccOMmdvpE7hMlhtQWn34eOxCcT7GL1fn2i1ut68DlDsXsywOX7MXO8B31EY2PHtsBZryiyqkxfQdl8UgAwOrzFZWgDKAO+siRsL2MYMyM2UL1AIBhIOdKsDUcATgfjL2cscIJ2aElJ6+wqB79P27B/fl3OcItIux/AaPhpPpSBkQLVdL52xk3jpg1lDwCUc9BbCtwDBCpDWHYf8wuBaKzmXT4Wsa28v9lfAuyBaGSX7Jfu2gRP7/NeL+kCbI6AC95n649MswBnuApbMvgaliYyqB2axslmXYV33nknfPLhZwTm0FQCL2jYsYudBwLzqWfYp8agAzPPSoGuh5u3bgWYA3W0Y+nCzhxOrHlugAPrFoYS4Af374fTRxLNFpBjWjQoycVxJrtHtqHXGzosKI3BnOXmXNfhwaNTltg9WpoTXw9RfjwJq611913v0DmwYSkE/+6ZVQX0sezEA09q1HnJCu5/uwWLcRism591V7P7sUzrdOolLr7Ri5nDRweA0kvM1WWXttM7weVzVaYrAj1kSDVk4lArxLUgkWBBOaUYAwIapM2H+Ua2UNHVVcct17tKEu16251Sk+1IJS7xuBt1RbV5jBIQdlHTunBHeuolmqazaOXBLc1F135SExBcnxxjAoTuUMsZ2gcAbaFZEGYlSPk9qkuj1mi0HsrM+/PW3+P4FwzAaGdiqUSb6dlnJ1NpS7fFzIFAaQdxjnTgKi1tv4KVpucHe0kHX/tCDkxnGlElAAjGXj4fBQCCgcD9aWdA0fVrR+Gtb387fPPN18Mzz9wJw8HCRMObtdmV1cYCeep6jsK2NqYHmmPgGR0eeLOOLFDMgaFms2VggxcAYwB+84N5mM6tWQ9E0zH/BZSKwQDbQF2+zc4C/wqsYrMz+QsOv+aHBdSmGWnzzpi5vF+WxA7DOJbAqgS9XQ6Mz0rvLz8PbCY6KK7WF14u7M+Ftgilf54gyHQHbb2197d0rXZdCijS/DC9KCV0YomONEI9IRuBKDHwwtaeF/qww46PJ7SvF4sNEyjrtdnT5TxpIrdsR2QAln5Zu0Q+HxMDOBMjsxMA1BgU/lFcv0qARmZTuVAcPPAvSPvwqsoJHT/u133ahv7BWJa9J0fT9n/SmD3dEuDHZQDud5EsShJ7gD+NR9y/NgNn607CJx9/Ev7Ln/8FgfzDGzfYxRK6dRS1H5rtwT5A0AHNF3a7cDC7ztLFv/nrX5MBuN6ipPeQQJYZcEucTWcHPM4HH3wUHp6chKY65Bp9+ZUXwutvvBHqZuHdOk3bEtp48AW2K7MZt69Pw2uvvxYOR8EAkqkdHyWtPE0S77PfoyyAV5tIE8/3keHOyiqjRrWXjmqfH3lCjsQHb5Zh+6g3WRjOza7qPNT6Rf0IKg8MCLz/4EGYTA3YOnv4RTg6Pg63b14js//hZ58yKfLSSy/RD1ClAiolcmBXwBnijNyOQ9uwHdtpPds80H5hjwCAka8f1+zDcGF8YXPNn2uvdxzfbJY1oKgaO1/8XCy9lL/rCSIvlYY9Z0K2Z72BqY5EMhKdeN6z6XUDv2BbkVhhMzpJTKDBijNPvZR711iX+HhNDvwpkMc96UV76N+L8bUz5MBUNaaYMftSc5cUB3Cf8yYlAtwhbWPzzNaBlTyjIse+N55Y0ypJXGh/ihUTlQGa9ca0dFEiTgYsZFbG47CT/ZFESYDEAyRq/Dxj06YGkIn9aLOS3+Z2YOfand4ICs/TfGsw2NHIA9IcTVhtkcxH+TdKjiveDxi55w/uMQEP/4+MXuzBLOm39VyztL+D1l10+037RNt+ppJTB5yLREZMxOg5Fr0Tdl7SK2Cv3AcEAMb9FAxdVp6ZNiMYn7ivH/7gO/RDRkMrtYYfDCC2FwBUc4isss2Ace+qDQB2uw0XKzAQq/Dq628wbjk/PyGwOqjaGo3yi+M+mAbM5ldnnWmyb33jq3V6GQMQ3+0HAO3I8fuSKMokXPJL7cOz9Jmysm4fADQ/KuIIfmECSHUc3VfJANzHB3xfKIDceJweiZOcCZjfv+yovv9bBwDLG44TSIh10Qa9vIEvAwBqQBgoO4NQIJYAwLzbUT5B+hhvsd13pBK3S9seBwDkRI4O25MxAK8CAOMGGktk3WBloGTrWXQ4uCUIyA1ZDI2s5Czf+Lue79MEALFxtEGDDmPeMkZPBgCWhqksXVtnzAn7bD8A2DUWMcj4fwEAyNuPDqrthHkXYN5/AQACoEYGGRo6BLIU+JEZCPHtGTegg/mtcP/e/fDh+x+H9997PyzOFixdO5Dm3s4o6YcHYwYC4wrBOIDFNYG6W3eO2QQEJSnUAByN6bAKAERXX4rRu3j/elWzy93izEo45HiJAWYNQQdhsLlv3QDPT63cbw2nzzpt0mnyABfAHxhF9WjG61zvrGnHsj6IJYb4HhxK6fCZWLkDRlqPdNARxJhGDMaTmy+YRWDuuIMI9gzucz4/NG1CL001ZkvajQGc0ilCpl5ZWjisYgpEh73dDUslSLheAYBwzAD8wRkHIZEaaXBCoIEElwTPE1oxYNr0AIClfRcAWgJfcZ9wwECrDoEduzqTgYgspa9K7/4nAJBZapStqOzY2T9dACCBPnWPKwBAOFgRnKH9VsLAgVq/sK8SAOzS+lBXYC5JL6FpmUE5Qj0MwORgqFQuFw9PJbxynmXHyueUA7v4P5ka2fwTYGQZbpUXJYBQDMA+AHA3qWgHZuNpuH79enj91VfCN7/1rfDsTROt3u3OzCFGYOAOMddUsyOjDE1eZKfQFITrBcwMaWx6qRgAO1wfGAoQ2cb6OpgfhMOjOdcx9gV10TYzaCXEsVRPJaGxRM2C1SgF4n/H+swBP5bVsezYOvymwA3NPypqFvJ8nqjaB9AraiQhWGATIC/jIqvu9JRaofkzFNPWAu92qXZ0GvcawdhftO9znJTgcYAyJmQVWAko87+XXeoFaCihAHvCJk8jAyIgLYB7uNcYgyYHIvNSPQEd6doTYNheDwZu/n8NAExj0A0APnkCtq0B2ft9nbiVFKdyUeux7EvktP23LgAQCcL/5c/+nIzAg+s3wmuvvcqkHoAaLCeylQByjLCeAHjX4fDgRgsAXK631PDExsmOpNCFHY/D7OCQEh4ffvgJGYCrxgCUr7/5KpsMNbsl1xn8B6wfzFvTzjJm8vFBFd5669thvFtTy6z2QDpWdkQ/0CsCYpMhm7csbXUmPub52JtoxHFyJmD0uzKgnXbCR1fxSrP2LrueCINNYokwmvOMRuH+yUWAJMmNm3dorz56758ocfDy156jv/HPv/olmWzf+c53CHqZZi5Am41JpjiwRA1fb55hds98mwoAYKwoMKYX/6bERmXyJPRt6JcYYxp220oeU2OR3A5EH4caiwYA4lXVADgsSOd1IGWbAaL077jXW0IIv+P5N7t2okZ2FJUS3P9d+7CpDZDCd7HfwK7Rf/QKj2jvvdlVU59Ff8yAA09Y5jp7LRKNM8RcH3OztXsBUM3rUMKm9kRwrEKw+WPNXayZku23nlCCrc/GAQl5Sjo0AHUtKcq17BqsPAYBSRtHMEsxv8/PHrDZXb3ekhmr/UwaruPJAYEkgpWYx5UB4RcLaPMuAjSj4d+CuGN+t3ev31iiDoAbElqYV9h30aQCr8Xq3ObTaEfgsV6vDAB8eC9cLAwApLSHA4BK0DWj1MRNhoeMQ2d2ad4kH6sgBsWAsBsATHG5AGYDzuT/DobWPCcyAH2elhUYIAaYH2frQ03ddiNUOo3DD3/vd2mvQHhgQy82UxxdCQAi3qL+oQNiAgBBSACBAQAgnv/rb3yd9u/07AGfs7TOxcz8sgBg8KZoyeh3E68eBwDMJUXyZoG0m0qMu3Zk3gSkzWTsPr+u72kDgGLklr4Ip0TmJ/eVAMdu4QXRLiWOnPGdd/7Nbcu//5/aDMDUNdAuSRt4n6YbxNW7XnECiwkQN552RvRqEcj0QLoyvDDodqH+uaLuuZOpkHUzHNTpenKml+4p3X/3xFAJcsoQ2catB5OYdimjYcf236PYfrtJSDmmMVDK/oDzpO5fbkeKDBhLl7IuxfG8yKw41d2GzyeKbww6TQzAC4AqOvQ+fn2OniZx13PIz5u+32aelc8h/q6A3AGRktlmFPv0bOE4tOazunz5hcntFPBbdjeOJYbFdN9mwsDSlsyf3b4mQjsA7iPY6vwk1QFIiZ5bvh6gQGwbSmznnmU4zIj4GIipmq1pftENcD67tQ7smdjGo/FBQJVrNCqDIadO947fjf1VUlLb6yhqT7JEOD17iS+jFGcKcfjqiMfbrIbhwYOH4f33PgofffxxGNVrYwAGK9ebV2DwAdzZEODBRsVmHZOGWmDXbljJL5xTWz9jc9ZQ/jmqCNJhA0XGHk7G2ekZS+fWGwBdWwKcACwfnj4is686f2Td6TLGKxxA3Amz3S5KvZXunmv44Di4hs3QSngaL1WJGlmeuoNDbS8bG5VQKBCoXOMv6ZJBe8Uah2DDnkxNiwjOAK5TAKA0P0bqUudnyZnB0gLjeYvSEz3nxXLFMRNgAuBTTEQBOn6DLZMmJuPOAxiBQnkmr7Qp0hgrbSN+H0VtQQNemrWVACMTzIBCmVfXfIMmE1lUZAYC4LMMugKA2AXYRbqxjg3Ec4dfXYIbcyoNiLXvSw+N8wvfh9PsF60AR4CX9g9p7iSxb3MA4v4T7Xp3oC77mgD3dgKpZACihJnLX6WixTKNZkNMQ08cxX3dGaTlsxBzrEx2iPkR32e3MmSwpYmUgLj8uyq5ZfCXzcHIAGTg1YTRoGGznx9//zss+TueGbNguzw3R3hg6wIBWQ541QPTBgS4BAcazi1+JyMQAfbYSuF2AAFYKjhhggE/LeACEI9rQ8Y8Mf/koE/GUwJVOCZeeCrIrAPQ5PzwgA3Dr6BKgRwC3ElVWVOB4TA2AZK4PRkWkg7KmDXW5AafH5H9gEQE+z26zQPTAt122SzJGccA8mV/2YmR5XRt1ndeAqwueGWFAe6R6z4SpxzYjb/3A3D5XEq7RLtiQ4GYOnJe1DuO76PlwqQGhmiWNIpdhcv5mUsF4G/oYt5+tf20Pb+l8IPAwCznutk7Z6ZKs7HcdwUEi1pcXIXWfQw8kTjLgXMB1Fd0WSyZwX3j0WVT9Sw7/1YwHfv8v+SB9QVW5fvdCdwUfxQMnEHg85+NpcU0DOdn5+F/+7P/alp7126TAYhSWwAHAI4I0Ow23rTBKgDG8wH3/Z/99S+ZGAjhmOt820BXdxSGE2PGQtsTn/+nf/4s3L93L2zC2BKMt47DD3/4w9DswDTekVkLBiI+i/Un7a+Xnj8yRmJljGRIqCh4Jvjut2clyg728M3ULMiei60jSI3kz6mreVM+H2lzYLf8+YlpHbURVVE1REf1YThZNOHevXvh+Po1An/3735Ae/ryC7d5b79+5z2O//e//z3TX10ZII+9NfcZ1IxrT8M6Ai3m5yaAUgxAb56RJy2zhkRraebVljgp/U1JG1gZ6TCM0XWD0ineZd4ZcPJz01xP8wz3CYkTvXK/pvJuxSoZrgfGyKxjItyaNEHrLY/XIIHD/T0mmpyZpvJiVkqktZAAECuJjeO0G1m5sSpDKFmCz1gZsvYXXbviUoW9a9kn3z/gD+G60GyPa3ov7nPA0Z/HiMChxSfYjybDYXjvvXfDpLJ1gpJorLvZ8SHXz3qHNTgK86MZ16EK4M4enUaADtca90tnumK+Yy9DUz40ySP7DQm2YBI6m2ZNH/6552+b9MzioXX/3a7C+WLBJnBqAsOKIhFXRm1WuxQm0rOOG6xXrrStYY4f0OzH7kfuf2Xdhg1YNMAtPg8nIKXJZeeLz5uaj2BI2iegBQjf3vwNVE0ZkeHf/ts/oP+yC9jXN1EDUOEXnqud1xm0qiBTV18Bj2ySiv3f7M3DkyXt6JtvfsObDoHhbHEVZU12DmDuVUx2x/F7vQo6mmnmcXtZzrq3f/VUapaf03NNeG2kDJj9LCrxyuvcB/4MrxlGBqePa2zWY1fQpf2XX5vWH58tgG//o+Ji+H95vN3yjb3Sy05U7qMeL7rPrApE3L/5BD4//+Q//KddvnnnAGD+/tMCAPObp8EuV1z55EoNNKGX0shwADCKwRYVF1cBgPtdY9oD+WUAQFvAOk57oqVAzDf3zLHrciRzALbLyVLzBE6gjnJbAYDUJMkefBSP93bzKrnlXNKmkG20ZSb2XwoA7HMskwW1rRCmtTW3JPLrBnivi68YSL5RKyDeN5AqMfMF3QMARuf8ku557I6UlczaRt7t8JbdmroAwNxwfRUAoN2xG7ZMi8TO6wGrNDV98HPnyMak2zDpWaGMzQJfy/ApGxy7fXoAXTcQ5x+Hwc4civXaHKYHdz8KJycn4eTBF3RApxCnHY3CeGTdbJ9/7gaPud6ecYOEBiAykPgMHAc4ZjgeNk2U/sD5Pzs7DRdnC26sYAoiMDhbXFBUWDExNgScf7a9sFspSt7R3AQv2FOWbgCUJMDkpTg+LAAAldmztec6Jf7A5TjnCQ4bIysJwvYroIiga21afgQMRuOA0kRp3NFGEFBOGotjMv/2Aw09h0776dlzHOf8YkkA9ioAEIyLPIAVgHMZAMhhzUoQ2yVMuZwCADzf0DygGGrjA8uTwtbmmI8cACI4zBJeK5cWAKguvrHpB5gi6BLp//A8co2/PAGVA4DGtgL4Ie6+7JOzsPzvXQCg7edtQ5Ns++UAYF56mdtDaQXpPQKwKIF5TABQfkFyTLsD+hIApJOfvboAwNbfxTDwUmHO1Y6SdM2fc9e+nAyacPv27fAHv/d2eP7558NkaJqco0FNxh5Eg/R8yMzxph8DiPVXXs7vGkG8Zgd8sd7xdwAMBNRhO5yVx3sZQO8rMYTB/DPdSEu8oTSMjAUHFMVYEUAEwJxl7mSMKGhAl2Fr4rFZrSP4x3N7I5lqYBp5F5QAAMBt39d+KQBbDEoME5m56By42YQl7N3WNAphn3IAEPcTS7UJUCQwWc9KdgTtdFr7rhIdGQCY28cyMdf6cvZLDgDKbsUkQfRPhmFbGYNn1dh9XaAUECWBDtyXx3/aAOCop4lOV1O2li9zBQBoe0HSbiNzXsn0wja0JFiKG/6XAgB1veV4/0sAgFhb07GxijAfH9x/EP7X//K/c/+bX78TXnnl5YBSR2v65ds1tb+wGSYAEH8HAIifu2DJgDDw7rRjm1cAAAEqffD+/fDg4cPQDKecdzdvXQs/+cnvh+msCudgMtVrAmbYB2Av1qtzHveFZ+fUCpxNG/oaMzB7M0YG/HTMZ8wfY10Z4MNGsw56GFDo+//MgUCfE5Z6TK/EvC2ejP9BDK3IjPdtp3Fx+9NloAYgurbCbzr5/GMCgM/ducbrfPf9D2jnvvXtb5sft1lxnEoAMPqBjVVGxbimkcSB4iG7TiaH8DlWZezvNdpHHM+LcWReuSXmXX7nk8YZ1WSuNWElf12VFLIt2TaLzylxqvPGfaz243nJcDO0Um+UUOMeYw8XMrcwPz1h79IrAoMGnsg3qQh7/vi+mrhF/16+mt/UZrUzmQhvyID9l2xDtNNwxndr/Py+kPhg4gqNoJgotQc/IgN9SICUzyGLV8wOO1NQDG8Bht7dfjII4d333g0H00Nr1lRDEmYbwtg0ODdhy4T0tRuYP0mj8PzRGT+vccX+jPtHN2HTwLSxODtbUQICawlN6+pmwveXmyU1O197/SXu1/XqlPEAugD98v4pAAAgAElEQVQjUQ9NaLyQCGCzPWluV20GIICSfLzEcBOwjNvOYxxVEOq6BQAKoFfn49i0qNB8H41MOoTXBibeHgOwnZAKbi+2tUmJwF7g+f83/+0fWIlzwPpDRZXZC/nFAADtHH4yn3+xm4qfF34eAW8HABfUKB6G1197g7IlW5ccEAA4aEzDMGnJ6m66/cPuaLetW/dVAIB6rsb8hH8VR92u/wkBQPWMuAwAhP1RYr+wvvHXVYEnlACgnp/8idL+5BJR7XP0A4A2D7xJzR/9h/+Zl9BV8tqVAShvJIpxF38oGYD6854ii1IRPRoL5cQqAaGo4deHgBYPWtehgOZpAYAJYOrLdBYLWQvOUyBd4J09F7uBPKOeD3XJANybaCoBENVbmohRhN+7gWVfVEay6/xxeQuAVROWDvAxv5YvywA0fYi+YDgHPbsD4zh+GucI1IgR6Bt4PEWZIWkDgOX4IoDOgY3ernz6orQXxHYrAMD9jIEb7t5MR+HwlSU0e8h9oYHUwQA0i2gMFTkAJQApXEMbRjku0hKJ9Oqe9Q2gvMX8K5gNmzXKSAdhiM5eKNscWcBHZwLdfy+s7f2uASi4DqtHp1bytl2Z8z3cWQYf5XKTMcXAOb8rY/7AgQXAd3G+oCNvZRAGXOJzCJLpsKLYBBojcAbBcvNhn65Nw0VOgTIs6tolh6AWIwSZdQcCcd3rmAH3zB/6dmUZwmpkDTsiAOiBowGAjOrj+XUNuF44QXCoxJQZjLyDW+xma5piueZFnrFXKaBKV2iDWDpiDDsBKHBw8LvsKZ0HF3LObXUEtFTK7OstTyzkma4YqBfUdpVukOXpgIcBx+a4yNwDKOFGh2tDCdF6xfECE5Aaf54AAIBqemp2PHUBFgAIxNfYVyi5FKiXug8OG3UVTAzA/HOaR3JqAPhzTagbcWNMZQRgDPidOSprJttXaraWdiZnCHQ6G0UzEI2vAqhE1G0HYnzurh2F/6dSih4AMAOH8XmWY2sPy3QB7bjQ4vGSLDlmOZMWpfQO5Pbtj6u1d5ceBHaq++mPv0etz2p7TsYbAhJ2v91Z0yAA9yzpgl4nSvNGLhnAYAWBnjGI4/NqAOhZORwYDmKOaj0kBzrtRQwsKQOwDcuLNa8DACKeu5WzeYdJMAoHLgKeaSFynDWOAzQrGZtswXjk3atR0j90vUoAeGAng1HE+sBWAg/H4n03tSU6wArx7vXsduoMQNg0+Rk6t7o1dyXh9DwU6ET/rkjA6rsx81wkyPq0hZO/0w7ISnF2MP6w3hAYkfmxqRkcLryEWe5lKqnq8890Bz0A+7BMDNvnooPfk/jLgcA8sBl4gKWunV1rNgcA1axC67H8fB8IeBUAKO25svS204bkbxYMQO2Bfd/rZ0KUz6M7ROxjAEoagM2bhlWYjqcEAP/sz/+CgNX04DoZwQIAzR7DL/BEmANCYADCTvzsr3+1BwByPQ6tVH48HXMdffH5GRlJDxZL+h/XbxyHN998MxxemxtTaFBZ050GLOIxz3d4eBTuPHsQjo+PwsQBxdlw00pyteYLfEvfS5UwSBIVxljfbC2xFdefAzaa71vXK8U1JQZUWqRbB/BHHvhHYM05ZihROz8/5z1gfzw/eUh7cnw05np7550PmCQFq1EMY9iVaoT1iISDSmdT2a1dqyhNCQDpmjvyDffSyA6U6TvSFhZQEwNllf65fzBkaSy0AO2b1dD8ydwPza8P9jG3fxEwVeIja2ZG/wjiKfRH7CiDkWtMxyYBuhOPMzQ+2GOdlUhGnRLvDsRKQxVdfM0GeFzoBBrEIbmEAT/jTaviGOG7vs/Y/SJZbL4rmkZxrNUczisdcv+Px/TxjBVVG0tAoVKFjPRNHd57990wHoIlDw1A+KBghFuctdpBK3AXbty6YVI3bqAB0mH9putOs4HazIMJE/KPTs4otUO26WYbFquBSdDU6zCfH4Q3vv4S98pmveTx7939mAB2UKUMfDwCgMaEiozRWMllWpHy33UVIohoLqQur/Y8EnGqnSzDmCgRrMoq+qmuV7latln2ewxCgJm4Hu++jbgF/hh1EOHb1kMyjX/6b35sEiEBepSbMJ9a13M1ORUAmIDI9jyM697tAEqrCbw2OM42vPC1rzGhgW7eVvGiigYBgLIp7ePGpxi1D9v2XfYO07nVI6GDGUirUfgXYKA+zqsEVnu/E6/TPhHtT3beFh7hczYeT1hFpuV52fWt1USw+FBsBui+M+OVHAfR/4sKgDLhFtdr3EBt/DVqgz/8H/+zMwAV8FtkFo3eFWLDXwYAzAGTkSiwjwkARmPmA/NlAUAEYkRoswy2Av+uB5YYfe2/6vqTCO/+hLSxbBvuSOEvSjv6JsvTBgA5CWDsvbto6XblIGDXwlPgPnINhr4ATfdzGQDYDjDS+OWZwnxc8nOl//cDgPyMxE7jBHKxTmk8+ftXlQCXzwfFHa1XEQio9KA0hBH0+lcMANo1OwBZlCDLQAkIK8flcQFAaFDsf9c73XKOGkCDjB0CagAGFNeGAHDdhEkdyPBZr9AFuAlhtSYAuFxYd8vJGM6JZV+h3XP3i8/D2fkZM2fMzlVjbnTYiODA4rN4KUg3wWLT1mNGFxlWMEyCsTNmmwvb4N2BgPPGzDO76O7CsEKJMRRSUjczsm4Qqzd1uFBG2ruYJlFgH33vokffjdp5cpJs3m03q8icjGsNjhXAUjQrGZtIMzLpGDM5gFpz+yU5Oq911aMTSV0gY0AAQIBDYM6HPmNi/bgvvAQm5M+1D0DOA1ubbvuAe55xRaAPMIZsKAC7mBsAW7ZgkdYsrSQA6po4Uxfk3tIhBPhhgJ4C0jHeewwAEEAsHWzvqCgmoEqA0j7cLgGOAKCLyIvxi+eB+4L9LAHAfBy+DADYZpQIaW2XkP2mAGAqEW7vdyVTwh5pkpeIgZkYmmI0OAAYAUY6uO2y39Ye4N+HRAAZrnUdXn/99fDjH0AL5ygMtudWprs17SnMc5aQu3YR1jkDhaHNITwL0xwd2DphWX4TDg6OI/CL9zCvEJCk76Drt4FxnPvsOLwJi9XSxNArdNUckjFNJrCvm3gvGwPskOAg8IifmazEeDblPNfaMmZIYuzCKccx1xs7X0MNJSvLwis69hLJD1ZKLA2mDa/HwT8GsNmzgn+gpiMFEK/rLzUM830+37/j+8X+mJj3mc+ZBZrl3pADgARUFeh6k52AgKnehtOVidELYEvAtTFL+l+XA4C5XhCOkQeCXcd9XAAwAapFJUNR8XJVSVQfEFgm3nX/AKbs1QO89SV2L6l06BqHrwoAZId72HAH9gAAfvH5F+G//sVf8q4Ort0hAAgmkbFxUndTjrmL3k8O0GhsHX72s79jQrBp0NHbGIAItgcjqyzAOsX+d3HeWAJyOufnzxdn7AIehnU4Oj4K89kh1yDWP+zIanluIP4EictNOJibeP/B2NaeOqfqKUSgQQGir3vZLdv/xuHgwErwzAfB3HHmh08jahrDvsRmUuk5c2+v7PsCxMxmGOCAFxi1LGGWZISX+E7HpgF3sbDSS9w7/DI2OFutCAiZVIFPryy+y+OsXe3X31PKXlYi5Mkk+SY2e+2CBcgb+JXsV2yOBo1k6BBn40FABVp9njSwK7ZxhP21MUFisw4bNAVh0twT6WK4DywRlTS5VAHiTJsosSTAx45vJbfYn/ys7nNhv+I5Y6BvA5m8ZfsdXYXhn6K5B8dhOIq+KNYF5mxLOiB2fXWbn3Ux5fflLlDqx/w7zS+OgwBVNQlx/3SEPRDNQFab8MH774fhzvZXAGMAwbBCsQ+CAQjw7uadm8ZUk4bf+cIninf/dXtEXckRjnVIYOvk4SNqb8o3OIfcHTWxN2E8noRXXn2OP03behfu3f2IAOBubU1pjJE7Cmt/bkr4Rnwkama2S4Mx2rldk93X/pf2pbYd1R6IyhJLDDjBACXgTNalzsQ267S3i0JvEiC4PvoArkkajzsYU+v0e9//Xfo7qEhAc56DGaRMHh8A1HM2phwS/QYAhp2t5zvPvUBNRdg3zLfKJYoGrkGSxubpAIBxMhT/+W0DgOX+GwFbv86433cAgF17dyzBzzZoyg357zFOy+RY+Oz1+ccEAPVctc9HAPC///d/ulOXOwu01AVJgdjlCGuZAS4fXNIksr+Ug7DPwPMj7DWhuBxY2ys1LJsqlKUazojIr78NYhXMqh6fMTl4+9fXArAcKc5LRdVtuMUg65n5JZErLrhsYkj8unWIyAD05xnHARt0cvbLkrPYXMA3/nLh6XqUOdQ5+xzZvrbq+1132uNYAoulc5lYS92Ou65LpbfS5NPvCsjLEuCUoSy63xaOb2kASmaOgL4uJlxXaUNf9+n+9/tMpYK/fcc+B6T13EVpRgiYb/g5AMh5WmRISs1Qfd6cMzzLy+3HsDJGGW1DBhSgRB2/D8eg/m8ZmDNgd80fZPyY8d/ahgWDCGdhvTgLp6dnYeVZQgBC2MDW25rNApC1JwNsNGbgq3kpR0BNJBhEQ0tvbCXAq42V+kFkmePHTOowHNQq+UjPgUG+d9FE5pMMAHXjU0lj7dcrKja0CDOtGHsG0Bv047tmTK7hSYdT3TrVnALAF9llpr0F8I+Z1Zj5tevkucCacaZnPv4MJjzLCMAR40R25crKC8T+s+94wOHMNpa5M4vc7q42iCUHfv74vSLgdce3zYRIn0ETFwRLKH/ET4h4C/CgLcg0XXGmCcewDs12GcXfee+e4R6rFMdLPseQSgBg48sGAXL+ecXjYvmhKx3H0z8Pxz0H4NR8Ja6p2EzEmKixRMMByxgvOWMgL921gNUzeN7MQes3aZu0x7Pl/HcE8xGQLBCCqPGmwM+BIa1maa6qeYNmf5z3KknLmCUISPIX/I5yX5BoPJgXtCH+hWhHs0BJ7EAGnk1DEf6333qDdmKwWfEnSr7xihpOKPcFKO6NgDBuWusE9li+m8ZwNp8ysFUpupXmJaaCmESa8ziXNQCyNTOeTpxxKLtqiQBo8CFxMac21JB2BQEUAmhojx0cHpqoOjoYkt1m3Tm1X2JscPzB0ADssTNZYvOhja1BfA7rdrkx7SEEhLkmZWRgesCs5xftgZfga06X+68Awtx+lP9v71DtDLQy+PlxLZBO+1Yp6F3ueJgzBIKxbllG1oRzsB3xzz+8doMSGYEewKhL4n4X3m5ALAGA5b7WZn507coMQPUHMbkL6Yj23ttOxF8F/unQXSDgvyQAWN4Df3/MCoZeIDIuycIvh/4vmdsDruvZZB7u3r0b/vL//L/JFJof3w4vvvBCmE7nrgHq3W8rS2oBMMEamc4sIfg3P/s7YwBuDxlQb9WlttrQF8A+Cr9gu5mSWX6yMM2tdb0Or7zySlguz8h8QoIQ11NvBwQEMRcxTwc7NAmrw3Ru63PaWPKAe24E8dLsGTExUZPJDuDJ9nYUOhsLeLsziQCBG/m8s/jDShy138eSMX8e6IrL9eoJmFICJ9S4btiRNnAO+0/7nAEYAGpgL3lf07F1V54f+P6YiCW6V/yhmljCAxIcsTIC53OWNsecSSTc+360AGCFfqIC5ghgWaxZSgEo3hx6iS6attFmZ36v+U51rPiwMZU2WpJcMWBVzTOMWcjakazRWkzmuEZwmXBt3L+L8VdjFku+cNKIdS3UqMVrwDe6x9JP5D4GcMaAJfib+G7UKPX9eOvMLZ0DzxWANOcWS26ht42yXdu/+jQENc8qiKiAUek+JRirX3zxBTWtuX62Df3fpWvosunHeh1uPXPLRtXDK/qWLPsFwA59TmNmxuYtSPTW23B2dsFEvpUAr8Jya4y4PQBwZ4lxAoAPAACaH4A9lkCkJ71gN/K9JxJtooSULHZBhIgDoL87cBctvP2eCGKq8Cji24L/kGu24/pT926zEyCcCFdghc8AEibD8PrXvxZu37oVRuNAxi4YgEjQ6/4iwO/PHwxE2mV1Z/YSdDWzoWQIE4vW0frZ518IN27cpIYqxt2JqSGWACfky0fG77PAYUppjBz/yWP9cnftIxCVDECTpsq+LWJGkeCKFQEFAaW8vtKe7l9X20+APVA8WmIX+bG0/kr/V/hE3gyok/mngymeKA9e/I6VpPlEe+p/H/QBgBJDTxnOvjN0O0r6dJ8GX7z+vkziYwCAMsz2s3g05feLphApsOq7/i8PAHaCVhkAyAmCLXxPEy65aXuOdg8AucfgQYlbnuFWsw8fcNV+24aWnS8TETfL4N1FrwAAZViuBOp6MsxPAwDMAa04S4uSt6sAwIiox/Hz519oYOyX3vUDj20tCSEEJcC5Z1J6FtrlQFqXcWmvj/SJywBAPPc2iJs2vhwAjE1joHXVAhbs848LACLOwzjlmUZen5qWQLQbm5WztNTlbL2x0tPl/TOeDyW8dz+9Gz77+EN285uMxmwKAAAQAB6ASjBz8IJTKeYiuvqxRHSDZiKb2NVa5bPY/Ah6OVNsyLJaON5GyQYAiHvdOqWfSweJFDfM7ALIjme4D9MNtM/YuG3U7U5q/mVGjc0S2iXAAPUqZxapOZAym+gKauU69rzBUrAMN0ph6wiEGOsN2mPJBsixKAFAjIscLmsc4MG2d23l884Cdm40Rdl+vs4t4+3zJLKZPEAoAECVkJquGoCSdia2zKBKk0WzfaAMvXc7ViCqfU0AIPWu4QCr450Df2LIxJIcNQEZuC4gSlwYuNkZS3uMpG0e8AiIhf1lF8PIZDdHNCYilJzJNtAcANz5flICgLHU0gfgSQHACMIX+01iUNmBuwBAMV/lsFggZA4rvyM2gbTuwKjNMluYF9Z8IutC7SBz1H9zYFTi7lzPVRVmw2F469vfDr/zxotk8kL8G/PlcDqywMhLkbHewZpDKQ0CCjAxyQBG0xz83wFBZNutfMfF5b1rNABbsmu9i+GU2kbW0RPdMvOElLT1YnCP+/UmOSwBr6ow3Y3CwcE8HB4fU8MIDEDYI7FsqwkAbgP/cDwB13DusS639Yog/3Q0M7vjbN0dVOFdTB32DwkQduP2eaPErLRxaK8RMCtxIZueaZTJJ8l9k07/Lm9WsQc620JRwmlcNJHTsQUAgq3RtbeJ4ai/yZ4r4Fe3y0cL02jdVNKstW/UXtI7EoNfJa09kjHlNeyTCL88AHiZ76RnelUibe/6CmbAvzQAqH0w2uGvCACMzCkHGFDR8+DBg/Crf/xnMn+u3Xo+3LxxI4xGLlYv5lIGAGJPns4TAMgumvURwXg0CTJAemXNZfyGNuuJaYuNZhYo1+vwtZdeCo9O7xvzKVjCEgkqzGGWKW7rMBlbN9LZAWTRxuGosuYQ+SvaS+yjW/MvyBTD+hRLzf31BpBA1kxIx9F6TiW47RmCxA0+Mx3MvGKhYO6434t9kj7EDg0HDKyjXfd9chfQXGUdG49NZwbYQTKDfpH7XKqsEXta80MVANKUjuL0koIp449sXts4GQAnNybun4orx+Y36CXAgf2Dsd/KDwFDy5OklE0ZGuAJSQiOL5h1lI4ZW7dZZ4HPUeZKVrnZT0kqaK/jtdG5SKXn+dpQAilKBUeNwPaKFYNHP7X/Ng0a3g3IdG7bZwek3DCKwRabRvmAAABMDeRsjynnYz5zyko4TBMrL7efqOAAQ295YYxI+LlIOqlSaugA0uE1a34HPMpK2aHDvSVwSeBxiH0PIFV6dmAAWgmwAYD43MU6SWBgvb362gvcTwc1tKm34cEXd9msp14ZAxD7n5UAm7ZzLpmR70uIk1vjIMmrUmIpJnQ9KVgw+EoGrPzDSETZtJ9ziUugwhvzV/uh8EKT+Bk7g7kJzz53I7zxxhthPBmExeIiTNxx1eiBqKMBp/+uZjOukc8+0v68MI+RUINdu7iwKoxnnns+3Lx5iwAg7ONo6sQLaQA+BQAQ1yfb96QAoPyBEsCL/okn6jWXY1OPKwDK3J7i/zEBruZ4mT5yPq6qZCr35XKvzwHAvGFonCflxp2Rtnhszb+eDT4CvMX1RgDwD8EAhDH3wEYMoJgJKka0pLpehpiWN9/5+yWlBDZhrwJI2gh9PEfJACzEqpNhuRzA1PH64JfKUxgJpW13rePm4VkGBsb+IPRe3tnwsvHqa8JyNWOsu3uYziUtGjzHNvCTsgOc+MV46nqqPY2mbke4jwHYxzi7Cj3PF2YXAAggID9GHwBYAnrDwpBdzc7sBgA7wT9S9G2lprK4/RLY9jy4HPjbY4bGLkTd81oaaqVDxMAvB22iVqFnimRgCgZgbrTl2KSAH6Ww5f313w/LYFxT3gDEXagg7l2NwnJV07FYLBtu/F/cf8BM/6MHp/acnVI/RubWmzvgejZL1yhRKYdnUMHAIRvG7wsbIkt9WTIIHRe7bzrz1AYzzUFqwDgDEH8/rG1Nax0rQy+gB+/D8QcjKAc5xczb1uYwg3LPVwxEbZxW9YVpj81MBwx0f/wEowHjsm3aAOwoat7Yxs2uzWDIoEwajrpv/JTN8pLg9lpKouIELFE6uN1Gh4udkFmyZCUdcqip4VFqvGVNYDTOUVTamVkIIFovD2xSZt3Gm9UPcN4m5ojHeRcBU123/S1l1O37ABoYTKMIBQ0gvERk4l0UzUE2bSg492iiwOy9Sqi8dCyV9Fo5cOWBAdRXrAzcAwYxEny9JxCwzSBVhjZqGBWaIKVDqN9T6aXN06QB2L2LlPZUJWFbD2kVgsaSY82jTKs0Z2BEbRFnLOh5qPQd81hrOL+iPDlrYJX9Vd9LJUb+vkrqM/ZgBKuaOkyDAXeo3P/ed78bXn/xNp8fmttgvc3mxvBBAIfAezw/4LqBTDqcRjwm3BeDOmh8OgCGuU0GoTvSstfoyobSO6xfzjPo/YGJsDSGbJx36BbupTssTffzIKDEC+U0+DefGDDBtcbue4lxTqahj6+aqUQGIMrvWZrozCFPKDQbleibphTLYN2Wym5xLrrjmHdxJ5PCx1kAIWwkn63ihw5Nzq4ZV8639Huh6Zv5FV0AIzS60tpRYgk2V4kmcfxUuuTjwe7Sw/Dw/NyACHfd126Itv5TiZoY7EV/1APovcRrwWKN4ELab/sC6Pb7bf8kH8Pu7z9ZApBzMwdLus1CJlL+eH5wPMxVfrt/MD7TrxAAxPPFukMSDEA41vLZckMG4AeffEEADkwkvFBxgNdoaFq+YM4SWJ/YWvmbn5sGYGgMAMT+hONtGpS1jpgwwOt80YSPP/4knF0MjLF7PA1f//rXWYIHu7HdGmg/Hs35eRzr+Og4PPfs9XD9+vVQjTe8rsnaEphcY1h/DkxHkXmVLIvZH5luzpDO1o8BYYkpRruytc+tGwCQWwJ1TDBQx3cbmqVpGG+kSSktXe8OultcmA1xpmU9sJLm3diaggxHE5ZOG0gGqRPTwDMJlZQMFqFAGoz6G5hEBqB5V9xa9sz9orWYRPY74jYmP+DXwAbu7vNd2d3YlMiRF/iPjLvIVDMGPX/yPhGVmLZjjKPEkJP/m8U3ijOYiFQXZddgHATr9ouSccmvEJgLloBA12n7aUCZ9u9YIed2X00d8Xk+T/kd0S/0daoE+cQkIAhojaowAegMkBad7NHELksom7Yt2IGjKI0wmsOXnPLZAWBD+SjnK2QiUCrrGpJ7FWJx0lq3efhLelGDFZ16uf/ZXllBHxDrdOwMshH8R/hlxkpHxQ/mDIB6k87AGnI9RXbPxRpGE5AzAlCax6ulLYg1yvEHg/D8i7fsfoJVzjwCAHj/QQgbjwucLQs/mN10iwRUau7g+3sBcNjzS7ZSflf0SdXEYy9gd/uT2UHua2VDJ87PJsYBSCTgc0hE0D7EUmBPPPu+f3g8C999++0wGiPJuIV+Ce+/iwFoQJU9LSQULXHbDQAuVxYXPvPMc2QAAkdjif/EpDbAQOS89gMCRzJfyZlwxfjtMQB9vSdJGY+P42yy/yQN3/YfKtdAvwqHEoCqMDYBg4ngkh85EhQU07Q6NyMB0r4ufVc4StLUbl9vOd8SIcs+VxIIIrO5qJ6NWE154z37crreNj4z+KoBwBJUKp5rnOh77+uN3wAAZI19pEi2u9WVmnw2ifu1YZ4EAMSx9KAt65AMhkDAHBTsu3cufG1AvZfWd2XafO2LMROVMSE5PmpX78BRdNiKsbgMAMwDf21sctr1t6sAwDQGpYOdlf91lbD5e2Up+l57dmU0RXkWs0/3H++3nQkl++ySl7pExY/IgPsbrexjptGiZ1KW0O2fKo1H11pCCJsHDMqk9pXWXwYA4twRBJRmoqjh0kzJAEACKgXok4Mz3LCuAAAJwrnzyJLOoTWR0L+T07Pw4MHD8NEnn1Hce70xHa/11hx2mQcYTjg4kyEaYFRhCNYeSnihEwWUAR1P4SSMTLQWJb2Hh4dhvbQSkF1tWmFqxqBusHA2mGmrvLTPu3mKYjeHg43gnVXBBgrhfuKGhaw9gEVvJqAxU9nzau0BtpekYB6zlM2RvNHUyjJmh1Myl1BeBOYAAA3OoTAxR0vizb7xytnfkClp2mPs1psBgNROU4AfkZnUeRCfr9kdGYwka7IivTA9sy4AMJ/DTTZ/bIdzRuNjAoBwHnlvwUq9opZhnKtqwuGNG9zBzxkBBPa0TgbQC7SmIJhj6BqNlyz0mBqDJtqOc9cbzxxH5p9KmbSRWgaegCqDwzYAmDOpUwCRSsgJxIEN5pqS0jKKO0ZkMjsg0SjT3058RUZXxz7RlUyRdm8OAOYJADgMOctKpcKlI1b78+Uacg05O44HouwimwBbAJjqdAgAa7MxDSRcP/eMjIlHoM+7Aidmnc0uY8PUYe6ZXTTw/e53vxdefe4G18t0YKWvk4k18ZhND8LB4UHYVSiNARxkgUc8L5IGzgDEfFGJd67xzCYAAKG9LBaB6OLsEa8HGpR4gSHI9V+Dfbshc5ndxifjcOPGjTA/PIzjgfmFwDq3u3F81bXP1zNYgCRHJRQAACAASURBVMwQOxMQJXJcTm4nYDcYGK+21EC9WKwYvKnDMYMd2AHvei2dQ2lHCayNiSmfR7A3Zme6X1cxAPNv2Ty8GgDkfakbp0skiLko+2l+lImRG3Dp4KB/niWKzS4s2UESsL8BMgAA+bt76GKUx3M+BQAwv/78/h8XAOz+/v8PANpYFv6h/FZnx4HZRe2+0ZSlcA9PjSmE0kGuT18v0ILFupxUE+5paMZpJcB/a37F7ph2BIk5dvFtjGk7gq7ocBBOTtbh17/+dVjXM37/1jPXw9tvfzccHY+ZKATQRD3hHZjGKBm2/fNrLz1Dps5ojN834ciBNs0T+dlaVwjQDbAy+5IH7rQbXgKr9YuEvn3QGExkYDuwqFiE81Dans4wXAtQ8sqkQbB1NWkaA3McAFzX1lBoNL8eptNJ+OTu57y/4+NjVlzg+VALDv7UzjrUmkSHwBzJvTgwWS9NwsCbJcBxMga0ae4NtN95qTG0m5mQ3FhJ9Xb7hSWamBBBEwu7fe5fmBPUXoWtUPzn44OP8js2LwywcHtCQNIYkjxWq5mG7/s+XpUDyqGZGPCBrtFq6ITvBmhTA0RZ+fXZ80wa8Dq9azWKAdiA+Wn+qD/QyATj8/Pzb2LTBtdq9gQWDSKAvIzyx/21kEKQHbROu7YHU0sOshm16SXb8KSx0FzVPsduw5C98ViJgLuAmWrG5z/y5jnjqWloYp6zyqbekbEP4Bb75WRsWp1VNeNzxt+wbpp6Zwl7J9NgjYF139TYj0cEhPG942sGSI2qrUkGXZxbE5CtAX4onccLWo6Yd9r/9ay/LAAY129RAow9x+y+MUD3Kt4KWZQYtwlgxpQcDsJ254l+18AWE5RdjVFJMKvC7/3e74Wha1qAOYxxj01AIuXLK3iUSPDmEruhKv7M3wYDEM8QJcCYh3duP7sHAAL4H3mCow8ALAlcJV5VoQke7JT7Gwkv0Ij69NPfC//2MgAwj5e7AEDOdwGXBYPuXzMAmPvzuv44Wo8BALbwgj/6H/4zbz2WjUaNAJsxeZAvQ2RW0QOSKKJiTnlvN7KObi37x24/9Px8WpjlJ/YGIH4gA93yUlc3zhpEaTjtnTkrCRGi3fqMgJBLBaXb5Wo2oG0HRpn9/Nj2LBJSK9Awf06RmeYBkr7fYnFlTLM+bZWuJi6aIDklVVT7vfEvMhj9JbL73SA5HGJQZpttcv4TwNQ1MzgNxcjKnns+BtGgXtHMZj/EsedUBkTlOKIEOmf75QFRDv7lCaFWIMDM52XOfRs81m22g4k0OjmTU+OTj115JgS4uUEpGUfoMkdmHjI76CAZtsy0g8lFR3BoXdzQJAE6NwD8AFDBAcQ1xszuYBczjSa2bEAdHChucPWQG/ij01W4d/9++PjTh/y5XECjZBtWW3T+heMgB9fmhmtoh3pnjpxldI3pxbkBwFtlJDlQNnTAYYPM4pYbMxyXqgJjD4CdZRQBPNGBxP/wrCJVyq5j1iy5sZM1RgclMeg4LhUYhMjQ2vihiYbNKbCukfXcsfRvjFJFAihrOsNHt47D9es3qKUjUW6cB/9XeaBNUBt/gSpYf3TMvGkKGI04P4YN9wUNRHwWIt2mZeafzzKhKm+U48vxX5p4OfxTOnw+pm0b4QLi3g2vZdN8g82BBnNw2ys7MQXTH+RAqfGCwEfO74xBGO0GmXv2fJRBH7sOkkqaamgyQczYAzCVAgFOhGM6GZl+YuXi4CoVUcbOHFHUCZsjqUYi6W48sygmQtwnTLPNmokMowh2uZ7z5lQlcBBtPTO3beZOCfbtg38ZKyMLIA2wS9Yh/S8xoXisUlJimBiweeYXJRY2fywwEQODpfAE+A2QKRnwbIaBbnb4590ZczvGZ0+7grL9TZhD6wgaUvUq/Lv/7t+F4+mQ62mxeMQ5ipLfO3fuhAlK/bkmksYp/i4HOY6pM/UwMfF3yAVQ46hBUDEK0Gy6WFwwIAbzAesC17ND0Atwzg0EfkcgfPvWbdP2c9YgAlWA6fjJ/c/BRJ0fAQobHJE5jAy7rQPFmdAEJRhI0BFAwyRcLJdk4agBicrmMX4ALEzCwJoIYc4BOKMuZt2E8aE1KcF8U5LQ5tuQdqaC1qaXu9lccnF6MotJh/AA34N9ZwIjsMoTn+ouWe5JffuYxkOaZehSGZ9RxsoBQ6J8iSmB9wEA4j7F7ESJMBM23mCmdoATj83mmx+tLAHbO0vO+Mv3aPnNyUaWX9X4lmORf07jktZvt4+QN1HhfLqEoVn6530dmPPruKoCou8YpYj53vA94RvJf2qPw6g2jVdtJGuy7kYEYpC8+/DDT006wjV2abcRAziQrm6Wo8qAh1/+yjQAt7UxhmGH8B2VlB9du2F/32zCL375y1APbhLIu35jHn7yk99nCR7WBRimAFWawYhr/+LsIf2f64ej8M1vfiNMB2BbLcN86AlI6ca5xh+GhwlJ+GexM6sBOLwHglrWxKRvn+X8ksZkZKy4H+7vzypnZvl8Z3dRjCekcGCD1wZcbpYbk04YzsL9+/fD9RvPMRn5i1/9imPx3e/9rpWPuj8PxiTnYmTSJSBTzwBzh21ZaM+0X3pis7b9qNqlLsF8zgM0IICuhjG8Bs700v4F/1V7M+372ABY2E2MY2SqUeoGdkBNGTSOxrSSn4IEPcY5MgzdXhM8pFahASYG+qFZXc3nerG+ML8L+q2ZJqD2L7KucF0bK/HG3oFKEVYmuH3Xc0WMqOsp1zcY3PY3/3TWnZv+pLT3dLBY2ePxjftPauqY5pKXVmeAO/1pAcw6XTCGZaDPjIQ79u0mLLemwbvZbWxsfAPLQRnq8TVeYl+bDytmvXgX8M1tLXjzFojc4XfnZSyXC4KEGHec7+zRfa7bZg3G7jhUXulTbwEWWrkz1yW0cwk8eQLepWfK+K7Lv23ZR+0XGcmH1ycmoJpBqNKI/osxTq3bs3fRFQCdSQnJ34K/3bjoXl3Z5+FPESCFFFA1DAcHVfj+978fphP312qLf0ptzKjpvpfSS36hzU2PawezcLG8CLODI2qpBsZ561BNELNt2YQmTi0HOvN9Lc7XTFoqH78xNuTspUSz3krxtK9XVSL410Z9XSj9AGXCOmn/2Qcy+MrsCTW80zVtVTqr+V6U0kYJtB58Ie3f7fvU/FAljuxkyQCU3aHae0G24fX24U+ZHeDzcImj9rMKYSAAEH8wrQJfcGJMZScwB7FdqtbXHTeeSJmmAuTYd3Ba8yD7xQbuywKAsaTJgaJWdgYOclEbHk/8JQHA0mHad5DaDky/g2UAARd7sYG0Fkzh//6mAGDvhOrr0vwbAoCab31P/6r38w3FPttmGHwZALAL8E0OaHshJw1E31BjqVRh2HpKqGBpHwcA3A/k7W7L51VSysvvlWFECaALAIxitDGlauLCKHk3IMYzjls06dgQADQGnO10dbMx5tpsRgbZ1rXyBJYYQAVwb8XS3s8+fxA+++yzcPIIzJVNWG+hmwO3e2psH4pxg2k3NCAN4waHAZlcHstLLdwxVL80ASnmJCVQV6Wo42ZqGXtnMllfSWvqYECYeRrIMPJn7IxrmfWDAJaNgWkmyqtSFXs+yGSLcURDXGQI0WUU2fPrN6+F+cE8NG5/d+hejOznZsnjxJJL1/6KiQN3kAW2xC5P6jYaGVpW4rw4X9ChFxMPGUQ4RQIi6OSVQWQD0MJKhzAM+d+l7dOySQ7c5HNPjkgOAHKcswwoAbUiI5qO68CZZ4AFDJQAoLoxCwBUV0BpoCDQIOhELbY6DHzeCADETwBQcKTwPAkAQiBbay1jDBNAzQBA0wJsM/MsGEnafibKnkowJ979uVzLXxUAGEuwfWC1f5fkatkJ2QFjBGYlVPHBmGOSmkfYNwUAYu3jZQAgGBde7soMs9uUDFBGwMp5gOAzK4kr9wEEZghuZgMAbaNwUO3CT3/6UwKA7H49GdL2zA+PrDvneEI7JY0jPhPvzmul3N75V46SB9pmCwYhrL373QDaXSMCgNQWhQQAAKXKRP9xvsOjo1BNjSlkwuY2B7gevEGOEjVg5LI0S1oy6gLpa3CzXlmJXWh4Xmh6sfTGm5E8OrHuv1q3+imQns68a3bRjjgoy3tGMFR5k4BsY7BEg4043pZuIT4/m00YaOE55SXLFsSm7qGUBEDA7aXSXQySrv2rfM5a510lPjbv2g6Q/B85+msH+mC3OS+pOevBd9OEtc89OcgpQEgJ2PKa7PevFgDcH5tuALDtZdiV5fY5Z6LHuZzd0FUg4G8KAF72jLv2mu6xTvtfmajNAUArZTOmEBhfp2dn4fPPHnA8hgMDKjQ22AfIKvb9FgAg/I5f/NIYgAIAEV8C6Np6M6mDo2u0IwC9/vZXfxsuNse85Bs35+FHP/pRmEwN+F27NECFUtnRKJw/uk8bc/PaJHz7298KhxM08dmE6c66n8KXydcP5zYAeg/o07jYPFDiftcYcK9XuU7E+I+BtNu1JOHTbhanJAD8OTK5lis2dYAfDcmC9bYKdz/9NHzt5Td5yo8++Ticnp2Gb3zjd2jfoUnK+xmCvQUwyJl/kheIXWUdIJ86gIRGJ7D9uDP4XShJhv0YWAk1O9IyeWoJXgCAGKNpxB98JYjJ6HssbTWkFSIg4uvIAcAIrjWu+bazJknleMOv7oqNmo0DSkxoswyAABMYVbzuqIetyoHEhjdgMZW56neuGdp3j/987HIfVtcHRnsOAKJUVtdPf132N95QAnrwva1PjP043j6Xl/byugrK92aLRNGGXXbZHTYMwvliET6/95kBr0P4UyinSCNq1W+eNEURMDQAN1alogZbYPzx/NjzKEHjCX0w3VGZUmOoIbNh8jRIVIGAEDg/hmHgBITxdsN9blev2lqH/nwGbDaSJ0WL+LEsHPTbkG+riqokkWHAderm64xPL/1O9tbnhwNgUZNPiVNVtnE9DAgAYv4BAOTPnWkFmzblkMxjAICzqSVOWdHkTUA0n+zSHWh/TACw3o05vvPDYwKAw7E1BQEeSQJIEe90xQO0B95kJ1tY/O+TAID4vLT7YpO+rwAAzK/RPP9+7b+nAQDy+UgqKMPJWgDuYwCArVi/YAL2EZBaAKDduGt29TGmdla7n7oRthkFUVMrimO2HbRkRPcdmm6QQ5oW5dSx3xOA0eUKGc1br5j9ywLcxIC7HIlGwNwStr+SUWZnfXwAsPv6I+orUdyMXs6F1VXylYFyyTnoPn5EoC9Dk83y7z0Am6B6v70Bd5UGtUDfeLzL50EvIFkYYl4igqzsxObwmQXfbzZS3k4Ch3JDqU/1AYB5rb82klbZbwGg7x0vK6HrnuH93XzjPV/iAD4JAMjPFpkDdVkzoBIBrNkHAQlH0wkdZwFIVWWi1wjMsHlfrCzzqq5ScNDh+J48fBQePHwY3n/vY278Fysridk1phlSw7FtmrAC4MQN0IJklHYpIIXjMYkOpgFlw4H9jM0ehmDt7M+xpjEwgFowHpTb99r3p8xuvpYtqHb74+LCKDXuCqZGY3QLrFmKAsd2MoV2zpiaKBifZ25cN82y+djYOWJgV9ZoY4XMpcp/ACgAQHEtH9vozTGIkgYxSwRQFDbL7geliXDILi7OXM8PJbBgL5mYcN5trxVQxCSEgRk7d8wE5AnQyud1/v0UdBlQJOCDQXiWoSznclxLMXPqAbkYEA6qABjWd63EwgNglzqANl8CXkxM3cyZXc/GNZjUTWzoDLHpyDvixZJe68CoLsMYc+qjDYwpKnai5klqCmJ7gAAZOUhy0vsAQNmtfYZ2246XXbjL9Z5+TxlEW79KWKS1ka9/Pc+kWZI0y3KGleaBjim/Q8dfbQw4x7ThZ70EGA4+xx/C396ZD58rRZvh8BnTsr2GETDj+xN2ba7CtYNR+OGPfhiOp9Y189k7t6m1NZmhS94obAeWqNBckQQH1iRBphhs+fyQppWXMklLp0F3X+gigQ0N5uFsHrWXWHroGmE1GdJNAta9RA37hUqIAUAfzFITANgjONcseXLmA47Jrns+DmSNZAy4xTm6kHoZO7Sf1CRAQHThCMpuke0MMXQHUqModlECm4vCc/0OXR8KjVYQuA1qliDZs7TxBSCL/RKBmzQEHz/h294Fta76NH688tr2pNzvcb9oU1vJE7jpZPv4wpTW4toDoaQNVJQkxZLi9nUlAFDdzp15IfvijOPyW+n3bn8s/3zb93kyALD0v/KxKat0LgMBnwYAqHvqmwN9Pp7tHV562COBgxIykwdwZv3Q7PZqWRO4Oj1HOSF8h3FkdvkGYPuR7/fQioP/8fNfGgOw3lgpMfwWMH3hf1BTFExiNhZbh7//u78Lq8a63B5dm4cf/+hHLMWjvcP8Z4ITAOIkrBfnvI7rR6PwjW98I1wb75h4RHsE2j1q58n2QEvPu5fX6IgKhm5bCkdjtqchVpZIF+MnrV7ND3SJNc1jyBZ4cgyBeWUJEjBk3n/v/TCuJuGZZ54Jy80gfPDBB+Gb336bgM+vf/1RODk5Cd/53W+Sed00xkSunVkf/UdnPAlIk3QOuX9k4qWSW67lrTMAq1nLr0K3V/7dKzwkoZN87tRRWXuxESi8xJXlyFlTtajhLk3X9rrcbh1wjOOqxIBLtWzgj4LRjioLMN38786aFr9E+3C+BmKygt3ujXmZa37ZumkDdonQYvNhq8tVSbJrRQJI1Svfo7HPyffCT3fn+NGcmab9NjZhKogeuh/4vdwjHLCcVKOwuLgIn33xOcdkfODPr2C6o+sz9o3hEPsfZDLQBARVQ878lzZwY5qB1KGklrWVpHNPJNi64/wDwxD7MppyAageAPCDXMd2RUASiVyBwbx2AZKy7yW7SkByz8ZTPs9SWgnzWuPMsZUfW4xDrFTR32XH4nNPGoCc95WVOMM6YHxByMJ+e3Q03QMA4a+jwoF2VBucJG8Kya88MZwzAFdr85uQ+HjxxRfCcDYyjcaRAYDWTMcYjXjt2fIe5p/m5kiM0tJP2bP38lftm7KPqgDL9838/6XmXtTC8w+JQZ/i+PY+KwCQK5H+g69K9yNid+V4Ul+vRUnzXgVmcb8lAKj5leyB++9Fc9M9XOaKEmBdZtw/pAGYBs0D4Ey7qw1oJAYgDgIqbavksuNJtBkjerJF4NHxPRsEs3DKpOxNMgUGfTeeMX8YIGZsg3xhlg+o1Z0lo7JrInQBYn2TsP1+Ecj0aNjlxpsTPgMAW8fr0S1sOcPZOOaswvz+xVIozxufXaSOt2v2flMAUIandA5LA/t4Y5uVphfj2gcARmaGGzA5vKVj8TgA4ONeo0o6OZcvLf91Y5c9v6vOsacJWlB7yjBChkfjnQOoOJdpoTSxKQKANTnccEyb1ZJi0OMpSs0QDKEUtgonp+csN11ttmT43f38Xrh/735YrJZW0toM6ABXQwgQg4ovu2IZYSTzUHK6q+x3idUD8AAAyBI2MAA90zccWineQAAgy/GMsVwCWi2H0B0NifBDHNzmBBg/NbP/eUmIdfKtqZFBkW921xyFKUTns+6mWk/bTc3SmcPjawQkQKVHQEEnGY4ThY9HFEcWAGhMRzGrHHj1zZXkNQcAaYu23h03c2TyjOZ6bYAoHC5cExiXAAw26yUdNYiEs8nIZBKbIAhkiJsQ78vBBc8P8Rl6SWA+J7uAf98y+QMBQMshLbr9lfZnr0Q4AwB5/7FJgO0TAgAxD3G/AgBRconz5gAg5/dmQUdm6KUaA5ayNGEKsezxOMwm1vUZmoCcF6i69Gy/GIB61nFTJXjkzCplfn3fUVdZ3Sc0evLvpf3N7keBX9pDBTQ407bIAHUBgPlzKtnGmmfcY7IEgK6vBQBmQYDej3bDm3WglCi35dDqxAsl8AQOXbMI698YI3BiUwfDkoElxzqryojTDSDgGCSewTDcOJ6E3//x74fjqTEVX3z+2XDj+nUTzQaLACkBdRzxIwj843PNmtjoGeB9aEIKkKMdWps9PJwfMNAAMMBSJoBeAJo8GBSjTKWw8HrIyGBDGy9zw7wLVtKPF5sMEKhEM6LUbZvzetcw0cKyOCRY6m04P18EMoAynVrqdkLj0Bl4CqgMvEOSws4haYbVZmXlZ979kSXaXCvedMc1MXVtkHbAZwlqwy5MhgzktI8SDGfHHkuswN/Sq2uP7wN/9J3LGIBcvw6M5JUetAvuZlpzK/RKtfUvvy6WBPO5W/dp/j2TmDCD0m6CkmxdYvqaf/z0AcBkC3hH6dTZ//pgRHw6ZwLlX+6V6blCzqbzAnrezKUcLgf47EGVn0l2rB1QlacDAMjvUzvYtH4BuJ2fr8LdT++GNbqMssGP+SfSEk0Bl4PWZAhvw89/+fdshLBZWxOg4WhswJhrewIAxHkW5xfhn995J+yq6/ze4dEk/PCHPyQAiL+DEUzdzV3F6xmgXG48CUezEF566cVwbepJo9r2Z0inRBkPT7CypHJszYny8cx943x/pL/TweyRfRNDMg9kVxeeMIX2cJaoohZuVZEB+Mknn2Khheeeey4sVrvw8Scfhzd/5y0CgB999CB8/vnn4Y2vvxwODg7DZrMwf6uySpEkIWf2LJa0qcR+ALamNSuz5+iVJI2tO1RQRFvDD9hxamrkmSSBfUcRt8lr6BXjIBA4svPrfM0QWoeYY3Y+NA9sJzBds1AMpsgwNJs3hCYim1UUAAVkFuFzoY9TVsEVr8vjUvpCrnFM/1kMyXgHbQAwxisOBMQKyqjhaGtJACDZbx3SHvLBUOLeaWek1ezXgVVWVpfhT/CrYOMBBHL8dqaleXJ2Rt+ZdhfsybHJYMBvpzYvdHlnszAIlgADAEgme2Ma32IAzudHZMBjrsHOrtfbcO+LL9gMBPv3fH4YHty/H7CPsQnQeBTefPPN0KwXBKQffPpx+PTu3RC20Jp01hyAR3XLy0R+W76T8ITHIPoQBykr8WIzkLZ2Z74fct17/AE/j+sjAn+SKrF5pkQ3tAC5LvT5ASqPxuHwcBK+9/3vh4OZl1R7E569phS+fvZxzbS/5AAgmi5ifh8cHYcXXnwxjObGCFSpf2IAtvenuNf3MP80va8CADF3c4Ax+YdObMgzgB170d5zEYCXzWvO/whrFPiMz2uRMGIllv9ntCdB8uUAwL4S4Picip4Fca6W87MEFqW/3ZNAG/zxn/xpG9EpNAA1gAIB8xJgTsxMOypHmrURlcGNMu00Hh0BZHpfT9MMS14CfJXTmM8DM3SYRG7gfSOPn+kdwLZrFYGzDNHOF2x+zvz6yoBs35Frn6fXWfIHkR+Pn/Xrv8zJMsOtzFU7k6jrVhCbb1Ct9dRjCBMA2H5eXU0/dL35PXRuKhmLSZTwjrVtb/U8v3QOv2/U9l/S5EXP5UkBwL7rKrsAlYG3FvZVJfTp+GmetMbPP1Cu77zkkhtNcaFa12I+KTMcn7+AmUg9tiMoY4IMMZ7NfDom0wOAGDaGTVOFs7Pz8I/vfMxMMQJ/ZNbXyuj5hTI7R1Ck3b01gJmCEpChlQyiuxfuF8eRA6/fGdRuoJOFoNqCUgCAfF+AhJf9yG7RGXOHiw4cRaKNeWMZLWv+QJtTNxTUt5dnuNx+CMCYjybcgGOgWlXs1Ht8eMgA4trNmxHgoL10AEQbSlWbVo66eaprL5g57LZXOYPNr4IOkjf04FXVWfmglxkKnKMINoFSAA2muajSbWT10CXx/HRBABJajGTrgG3J8TaAFeeT02Ii217C4hOqpPbj7VI7tDX1Wvo6hQBg9sHLgPF8T0EXQrzKtU3OhAOAZiasRHHAbtEprzfYGSCKkgnac4AnEIVHV72qCkcH6Co5CrNRao5Ch9YBPmkM5kxqXg+qg2MjA2sqodJ5Xo/bosoDDziAuQ2OzOW9oFz2zAarrIDYC5yL4GMPAHRmBI6V2xXst/mYoomNrYL2MxMDUVpJeoT4HMZp7cCracqAceDrPQLIXpKpRIXbg1SyliwbAyo3bBY4NWHiDs+Na7Pw03/z03A8Nk3N68eH4eDggIE4Ag8EOgr+9V0Ag3kTpXLflj/D9wEC1HWYjScs8aU+GOwTG34AzHfpADxn/FMJL3T0ABC6ncGapN3zDPzp6QmDH/wuVp40aJhgcSYgxlXanxQ3r0M4Oz8LqzUSHTZXWUpFMfdUgqTnmhx+YwJDA5OJgMZKqcW0NNF8A0pwvPHIuluiJJCJAhfrN0C9CYORGXZoNwIwl+6f5vIQjRB85uQAoO5/4GBoy0Z0/MLx8zmSM9ZKIDp+1eeFAX0AAD3xIGAwS4xpP6HN988RMCZTRIxAL02MJ5Bm5lfHAKSd2Fv/7Z38MgDwsjH9qkHAUsu1D+DL37f/t5nKuoeuShfOu1qSCl6KD13g0TiAGYsuvZAeoO31EmCVxJoof9IKnk4AXoEB+Pfc80HyxTobVBOW++4GE+9mapq+63pLhuF2MDPgcbAl8BArkgCoARgZH9COgjnM+9uccg3NK9NEPp5jbSGIt+7i0OadQRPYGXhonpEnSNIzdcB54DWwsTmbr0v3V9hYDfcphrO6vnvShX8D4EIZFSvDxEuJsllV0U84O1tQS/V8VYeHDx+EW8++wM+h2RDG4YUXnw9HR4dhtVZTDbeHuXan2/zWPkOtP2Po2bnbz9/2zCFLkPkamB1GIoLX3RHXtOZ9JC6oK6rtX7pP7HPmB7qdihR2AZKmIcpGcjy/9i9LYEUA2hm/sjeMuLypVF5hkl8b7U6RlFI8FeMDWKSWLIrHw76/yn5qTGPCMG88mX0fCQ59No8LY/yj7bYgfPQxsKHBGDVkmdAGO3YZzi+WBPhWaCix2ZAggH15va5JCEBCCV1l2XcFHYhXVsoryZHETBxyf0IFC9YL/HQAzo8enfr+NAtffHEvXKwWpoV3OAvf+ua3wna9CLP5LGxOTsI7774TtssLS3Bv1kx2g6nJ/b1k5JVG80ogUAnP0hKL513onwAAIABJREFUuJTGO+6JrPSxE0W3x0FaxGO2z3lCCjkNagDavEMTHmOwmn0B5A8/BgDg22+/HQ7mzkxFBQI0KP1EsamFA3J7xJ0sQsTx6epDy3FliRUkPp5//oUwPzKtVJT4435GPcQrxeV980bDDPtMO74XCiihVtoRb9bj/qrs1GV7Hde7xrsIlPuAP3s2bQIa34uJ4rYfvn9+AYHFvCjGCxrPuT3cx1O8z0ExPvu4UrqC1p7qidi+/fOxAMAE/qVSM1FX2wCgUYH5QCMlNIkhc7MpkXIhm71PEMyTRL2kDX7CTKUepBagNj1tKK1TFyUw+bkoPu4LKD4AdPnpAJZyZ7d9ayUU0+3C7d2jZ3zKB59ruHWNSzL2KdOq+y/BxDIA6tpIuQgyJog0ttJnk+ErHymPX1CCf1MAkCpA+XwoKbJyENRVsRcElOGVYW4bnj4GYHtDTyUcyQHuztwrE4oubo/3as+T+Fz9y10AIMZFQe9VAGBrHWfPl80/yI6w8ZhAzL4akRHFjWJjGlWL1Tp8dvez8N5Hn3GDPjkzUV5zOJrQDCcMUms2sfCsO4M/3ZcFzWICiomBwBL3KsYdnFQ11cD7k6iV4Rnm3YVnQA0QNBYgwBXvgumARHR4BDCiVIxdsY1xZywYiBK3A1hmtRFsuyP4zM3b4fjoiGwgZvpbWl8mkm/NThxUJADYsAkCmDoIBDC+mxpZTjjBXqYIgLUaskQvXyO4XTIF/Bk1tQGl7Xlo169NjM6udyODP47MPtiVaGbw8P6JOUKDnWkYeqc1bZj4qjV9sSYm6hqOU3IOFgB8FwCo/YOAgUpBHZjq28jksHTZtJZNHqGJQWKQ6XglAIhZxjEBo5Wl364xU1nggWYpnJfeXXDsTjNKNPFc5wBJ41ygS9Ha5/ISLe4z0vQgUypnVyQtIDIX0AXNS7zy/WlPjDraOF8vfv39JcBt5oDmRzlXym6H+ty+1fJATRlUdQX0AE8AoAVzAPrsJ5vfOJBsAZvNV30O60lagNzbxY6TQcuaXAGILRnLKAHBuL34wq3wgx/8IBwMt+H2rVthPLKAIYyhWzejw2zrSzbaGDrQEtV8kr0UuxifRddffH82nfJ408o0PrF+WCroouRy2MX8Y1BJFo0zyNjxGN0t10yYQEMQYwCGLtefdzWWXIGBAuOwQ7fzNTpImoA5j8s5ZYzD5doYq5o7ADhkR3WvDNBYZgXm79akFnwssM6tSYYzWMT0LRn03uVatozdkMF6HFq3wcHISiZVDszrZGm1GDlp3Pmcte3lbZZ7NkKOT882KUB6348wv1MBOUoy8cq7ZeeBMPYb8+/Mdm4UnGcAIO1p3GifDgPQbG9Per5zPNors2wC0r9+2wfrAwDxqat0AR/HX/kyAKAdt9tfehIAEHPw7GwVPv3kEwJ3BNyDaaUlP8fP44wYAIBYHz/7xd8RAFyvdlz3tUsWVCM0wIA/4YmaasjmO+sAML4JJ4/uhePjo7BeW/MDlBTjfPPDawSwUIpHW7A55XnGu7V9LqiJmN099mH6GHEcUuKRTHTYAKwpL/EcjqwkUPYnSp/I32WSdZiADt8rzT55x+TsmXN9o6LBtVjxE3bs5OEpfRyovGGfHE7mvM579x6yJPPZ5+5QaxX7idlR62IbtU2zOZ7v+ePRzHwLxYFoPhITZ86KYiLPNfpcw1fSJmUCrIy78hJkzu3IsDLAE/4OXpXPDwwbmYJ+PWAa0j9QIw4HVOlSoQJlZ9el88K/JbuaST4kspUwTRrU8svyn/o//B6MhxIeqCSwpLXWhe3DJWM5jhmZ22bLOE+UFPbvR4azz5pY8eN+HM6S+3W6rzLBGe1MfWHj4MxyNFuDzvTD0zNjwIK1RwBwSoAYDD505cVefufObQKC3Ic3xgy0pnTDWJpMzWAkZAcAxmecewIA4a+iCy1+X2/RmGcbbj9zM7z88sss7ec4LpcEAOuVMV1Dbd2BjRm+C8NxKpXujJ8FoPXGadrXSrHAbgBQyT3519A25Fr09QpAy9aH+5fQ+HMJJFz+prF9H0QJanSSZV+F+cE4fPftt8NsavGGzSMwSj3u13GzRIENSNrH4jPNAMDNFh29t2E0mYVnn302HF47tIZjA0gTVGyO1vmK57l8t3hSAFBxqI6qyp2r9qQnAQDbMYmeY7SyfqqvBgCEJmjXq8//KTGcvnEoE/f63OCP/+g/0bWKk18i9JFZ5hMkAiduVHxE44bqBibWLGcOGyY9mCw0RoWvo8x5fKB7AvRuQLPvPZ7DlBhvfYNE58s1IOJC2BNP7O7mFmvmswd2mVOVHszjAYACWEtQbu9eigC804i59pKec9wUsix4+b1yI1XTgHKC9QKAel5ZBmXvmNnByhKeqxZ0/HvBgGxrACbmpwCFPhZgdEDigUsqcJyhrUvTpxRYy5zGsvliXeRfZhCcbSxdwUB673IGYO/6KfTvSkp0ZAD6XIjlD8oAssQWAaQxxBhUhxAWZ6fU3vj88wVLQu7SEVyEzQ5suEFYba0pxtYdVjJEkBEluIZAHN3VmrDdtCUEQoXmC2NmxVmC4o0CwNlSYI73WFIIB9vtDDVhCEqYCLVKebEBmgNlzqT9w1PzDNvG7UvUALMSP9wDgB/rgjwM04mVLBzNwSyCjh82YAB7YADapozs5A5CyLiHyaTVxAfOCZwXADZgGMa1sJQumXdQH3pCwecFsm/t6zaRYgQiBDgRoDjrhzeu0pCoE2YAlxxLjg1F0s3hOvn8AcEMMC1xTSNp35EVCI0wn1no/upaLzyNN18p7QYYrVpjuXREWlYJ/MF9KKGC2d1lu7pEhekwSfNzgBLdfQCQYuJsvmE3AC0jAYB8Q0DQ0Jq4AGhB6RcyxXihtJTfQznwdBoOZlOWbrNrtZeIcyy1vot9K0/MMHCTFimrIz2IdNFz2mXXokuOfFrv7XHJ7EDRLdCeSxsp0X5RMpv1PNQVV58rm21pT9P7becIq8gA/rwJCNkkDnRr3SFQ43edacGkgIv258wuJAAYEKKEqiitERs8BzFHASy1UfjON98Ir7z6SpiEFZkqYNCg9B7A1uHBIRlqtCWR8WHzDdet8SXzTV2C/XkcHpjGF4JidhcE+weMILAPoe1Vu11x1hzE5nG86QiNSCbR3mG+4dkg+EdXPTT3gP2D3wFHnmw/L0PlvHJgFWsUGka4bivncuCauokAAyYsj9IYAgBkAO7NkizQRwtwYzlvV1sLfOP6sZmAgJKJBXUF5/eTMyrwEQEUXtPx2EskrZEL1wzWx8QYCKr0SH5llhzLAnxSZS956X5LQKmch22WDCnBflT7Ka0sG/P0Gvq1b32+4XM49oaNLbdJesKPB2Zgez0+OQPQrr1vfV86HHt/RODY5Xf2xWU6wL8WAFBMvK67zv2h3i7A6F4NH8dLPLl2qiqcP1qGTz/9NAxHM2f62R6dA4Dy//FzOplzHf3iF/9IZq0YgI13PR9WBlSBmURtwE3Dzy1dBP/s7B4ZTWh2hnVfja177fzguq3dYOtw4pp/4x26kjbh4OCmMaicCYK4wuZXSuCYnTI7Iy1VMdGipIUAA83vopKLewN9H3fMnfmEdIbZCyQCanYbJ/N3sDH7vFqSAbSuN+b7qQvr0BjQAE7B+JpMfHybLUs/+Rw8mUgWH0o9XTue1+Haf2L2AYejzzdG0hX6yJZAoJwAr9E146JUjzEjq/E0MqbwOQO+upikbn88AanxUJOMgey4+8taV8PK/RUlXN0ug8mO+4eGYtug+HNyJjEAQb3kx+H3PTazKm5cukZAR572N7/PE2uU0UHhuN1vbCJBjdmKCR/4NZu1pGTsKuQdRCAwJvLa1xntRIY86Pnl91sNsZ/UYYRcMIHaUTg/Pw8PHpzw9wnKwqE1PRmTAbhZrsLZ+XkYjybh+vUb4YLdekfU9mNCrbFKiURatKZ/ODeabIFwAMDPjoF9cx4ePngYzpcLMgXvPHMjvPDCC5RuYdOv5Xl47733w9ClLgCwsDsx2HFILnr5uGx66YNqvfQ1t1AFF6Q1iongA+4AUiYtofWGD0zQtIUl29Z9Vhrkep4ACPGSBNLWmyFCmoJ2YWf7LboAf+c732HzOqswMKIEjsh1oQffAQCKcZjLg4kBuIPFQoXMaEQA8OgaQFw0EUQDkg4AsAf468NPN8X6EcFAY5kq2Vx7SMQy/8BVgFmJN5Xxb4lJYNxz30LrJALjfl5pCceu2I9RKm7rr+2XQC3fztfNfO8D/nKbYvaxDayV8h+lxE1c33/0h/9x15r8VwCAZcleHNBowJKhjQ/RAUCbielCuaGJ8ZJRxfMMpEqP+rqYlI5gWoQ20HKac2ciH7zHAQDLAdYDI7uo7G58aZkpTX+3oSjeFQC493YJ2l0CAIoBxY3HH5RKFbUZCQxMBj8xBTlhYzluNzL9mwKAOr901sr7vfL3S2rg89LvPgAwNnbZW8CPDwDmAWm+YRMAuQQA5GzIzhuDnc5sacrmxfmLNZfNN4oQF0yJsgFGFwCYa57kACD+D6YfgS5sJU4nBvD33jv/FN59791wsazoAG5cjHtXWcnkEhFVCGEJ0MgDQmlCsfskSs+wCSPgL8TblTmmAzGxEhc4viz9YCANwMEcrdHaSoCxjs1BMQAQGUDOLTT4ZWDdnkksOQGDcGCOukrgIC4LB/TmTdPsQ2kO17+XDAHIwQYrwBjrKuljoUmGAYi4NoogxxI4O//OHQUTNN6Gg2pkXY4lgl1ZAKB5wU5nmXZRvA80ufBundL9khaVOgryvlgqA+ZdAt4AZoDZiYDn4WcPCEgsvZPoEN1MJ+Mwmc/5c70x50tAjuYXNFro+HkGXXP3SQBAG3cvISyeT9qT9qk/5vCa9hugjD4AkMAHyp+p1WPOs5rYJFFkBC9jMrAAtKzPz/icVFq6WZyzJAulwHBAxxPpnVnpJZiCLFG6pFQP18EAkHXBBtBQ18mZqfwZfzdgt3SE4uwtSwgKA/m0AUAdvpQI0PsA4u052k+JrQMApLi8A+8qfQcAKPAvD8il82Tfa+iYs2THj5uDfzmzDQAgHPo/+NF3w7Xja2FYL8Lt27fC4XxKTaqzi3Nj5yJod0CLZarOTJTGIO/By21Rynown4f5wUG4WHiXTu9yi+TEdDLlPVCYPFiXSdocAL0jMBq34eL8jBqo0tMEA4iaYoOhi5pfcP4czI8iQ9jmg9m1zWpNu4pkAR3wzKbg+skOhgj4yJsMafyheUgNvsR4xvUg8ALzDwE3/o6SKrwYCEF3kCyXmsAhygZZMg1AwLto4rMIuKANxpc/98nUtC61B46mniQaDtr6jj7v7R6TUL8c6WIa7/3apYG2B/rl38oAQAZKftkJyLZnhvnDUm51ghxhLQ/Z9ZXve9ANRiDtdKbz6gPhLCc7gcZB99h1X5cBgKU/dtW4xMC/8Dt/EwCQc/mJWIn7V/m4GoC/KQA4HoytC2mmIScA8LPPPguVA4AIlAnwx9I7l8ZwiQ0AgGBu/fzn/0BgTwzAMBhbuehw6j9HlBY4OTkNH3/8cTivrVv8dDZgAA4gDNrGkPXE2llvjAGN/Qo26MU718Pzzz0Xbh55V8+N+VXwMswuqtQ0BYSYd6iAADPxbGFdv2V3+wBAJETxkuQHroEMYO+2agmWYRhCW82ZavTPGIg2YVAvbf3WW5YAg+HLeTExSQBo78KeTMbWpRd+A5hCeA7wn/B/fC6CgF6qDFNJf8WlBrywJHZjRndfu3CbwZSDccBczEj67M4UjJIqKpWOycA2gKqEXA5EEMDQfaHrMIBat8Gwc2zONiniIQKXADANgEkJ/4ExoCcoFWdYT/9I/rniLfyMyTjvkm7AmZ+nYH5KuzhfYfQtnXjSgBWfMchwPaZZ7P6hGwJVCiQmVNuOQ7tXr/wa4SfaPNI4FJqBOzTnQCm0yZogUlheLMPp6bnJZGAsXBOT2rUbY86PqjGlNOB3Yp1Aixr73dY1PWtvEob4ADEM9jtqCG6s5BxzkpITgxm1cNF0BM315vNxeOPrb1CznMm6s0fhk08+DoP1ypt3bbkOsdaRpBdjXnuv3WtOttCopARW61nEccsjP64I+5j7awKSUHKcA4BDp6ZDq5rjN/BS9XgS8yfWLnUALUDOvwgoThifzQ6G4a233grTsfnDiEMYfziA3gcAgkigfawLABxWB2ZvwoBNgI5vHLOKAc14KH1UEEziZTkgrLjgaQGASpDoqr8KAND2cZv3lwGAlHNRs53fIgDYB/7l8zQRANrx1OBP/tg0AOM8KDJHebxhE8QRbemxuFhrPJm3V9fv+yyQtmuSdyHq+o7YJF0agAIkSuNovycGYP73/f9bgFsiwRGAiyVI7YxtBMY8Ix4NZFb61T5XD8LLTFpHkJtlqvIJmcbIl8AVwFVyKNsGKTf23ePTziz3Fao+SfFK13lUypBnx/L7LTMv5Vjlkz//WwmEaR5FMc94Q4VhL7tNuocCg9vFaCpL6vLz0qEoNLPi8/PzRxHfQoNrf6zKDcY/UWowxOvXfSkwt8CSK8OdKlH3Ofa1iSojAwWHF9eNAHhbe1e93SicL87Dex/eC//w938fPrn7gMcBgIONfjo/oMO02ZlovrSpEhPDxdbhDHinyrI8pPX8pLXiJVjIpMKBReBJXZstOuxtw3wIMAssPztus1tyc+d90kFrZ1zg0FCTC112B4NwfP0ZBraTsQXo1QgBsGXkOQ9rc0jlGOYAfp4IsPlrmnn6fNe6xXuYg7oqOH7q6imJATJ0hnadAlQE7MjmWSa4CdUIXQktmEkaidCs2dBxwnWrhERJEDioBF9q6LGAyfkwLBcXdFzZKQ06StApmsO5T/YejoiYXOzgRmA4dZWzGVkwbGIX4cS81LgQOFETjGze2pr2+duTUIlrcU9TQ938fBdwglEsuc8kJwggo2SJjqsBT8uld5jbrjkPmvWWDtbUSxwR4CHgU4DQRM0jO1+UwFBHR99bMO8M6HMAxB1BlMwYI1BM8/Y+E7WPSoOgbt17DKoicRFZry5mnguCt0TRjVlAoC8D5DXfSqkP2evEgJaWlK8b9wTXXO/GPOH6UAClPU/PNzrKfqNi5LrmCtluDo4huMT8QOCA91Dy+/23foeMv3G9DMfHx+HadEYQDxkMMHfRjZAlM/XSJQhMNy/swPLdkDGGz4HRgpcYxjivtKnIbNyYxABsBbuNikHh7EIwRvG59flFWFwswnZlJbJ4TACQoYknUJBAZOUaew4AUw5gs+H6xbwwwW3pQxooZYC/af3tRkcWAHqpLUEqMqStFK2G1AA0QzdoMgAwXA1IbJz7SivL6cZGzASuTRJAzwNMHc57MueMWc3fATCwgYgB39qnkdBgKbc/S+1FVvqVxPzFdEkJZ+1j7VK6Pe0qv3ArxU7VG31AoxLQ8X6lHejLEExAvKD1iP1n5eL4jZdjJwDGZB6QkCDj2kviZHO1t8E+KDHLJg+V2VuxglVpYRqj3gHSGe+twBTgBfZx9z/z55V8UdOnLAOE1mcvSVg/DgiYJw/z4+YlieVcyn/Pu092fW7PL88+lI9PZNDWxkC7OFuHjz76iOuZfoEDNrBAbOTFBCEmre/vY6ts+OUv/5mVDU0z5nqFJAHBs2ZnFQHQAZ3N+Mx+8Yufh009I1A2O5iHn/z+T8JoakxjMNP0nPFzPDI/CSbpxRdejEwdJDAJlFXSJLQEZrw3aVd6IqXxpgnDYADioFnY843Nl8RYs3WpAF9+ibpnat3kIv5m0610El1T8ZpNrMTy3fc/pE+wWJvGKoAIW7sD+nuqDNkC0Mkb/4wMeBVQByCBjHHXHcR2DCAfDH2uEXYkxnV4d2S3/4oTpbmHvCEZ/S4ptNuhWy9Kbr0E2b8HD8WOK0DQtAPDzn6OtzMHzCxRPUDJrwOBZiCNmU0tSWisOiMQfi+/79qvTaMmduZbyd7skFDwBDjtXmUyMSp9HA0s4ThsvIKlWdLfQOIZ/t/s+JpdhvthKoWOccbIJE0YmcOXgJ3OEraVS1aAycXjOGM1Hk/xDVLgSHb4fNO6riALkzHxaVMzmwHAis8fPjP8atcChwYnX6MJNW6r2dwkFhprCjibT8Lx0TETsqapadVBzQ6AO5j4BsgCwAKAiHNi3wMjDt+njiD81dncfj87o33G3v/6669x/sK2Lhf3wicffxI256e8HPgOpitpFTylhr5KtmVmVLIbzU6RWSnzJOU+k7TwfS9Ss0M/oABWAdqRqOJ/R0UE9m4AUdwzgzUQFFMfjYXUbObV114Nd+7c4FitLs7Mn3d5kJHHoTFR68fX/iXCgdSxpdWMBAifz7ZhYvX4xjWTABhCkgTanL7PFgCBSu1L4K8koOTj07b/bcKLMfPaEnO2HyZNwHL/YCn5XjzSjqNrb3ZbVhjkmsmcN4rjvcIqao/6B/cATlXQFcxhXWMkYmdVH/vXD1p2H/Jin45NlSQ1oJ9+sBwjoy9SEi2uAgAxMdrAR3sAhx0AYHkj7d+vBgD1+ZbD85glwAlEeDoAYGz7DDF8H9wEqgEgsA0nXvMVWgH5WGhB0zAXWjC5VlacNFGX57Lz9eV+nxwA5Mbkm3mfo/c0AMAIpnYAofuASxpBMcW65ttl1FkLVDSPHx8A5IIr2lH2AYC6p8cFAEtgc/+evhwAmMbPRW8zlgk32o2VssEhgmGfjpG1HcdmCQhQkZn76POH4f333wvvffhFuH//QahrY3qsqZFiDh5BKA98An6CqeWlap3PiCWsCJYcZPJulPysG9pqaiVuCGjxEhA3obNThd1ybQHzFg7gIExYpSJQ0LRU8DlkHK3brQF9k6l9fzQ64PegGWbsWAfqvQmGAoQyg5ZsVGJ98LJ7AMDINPW1ruMhUysAkN93h5bdYl1LTmBLDgLaXERXXytFMc3CJCgLLUH8fnHuAYKzoeEU4D4BnkLcHCwldFQDAMj17qWIBEXR3XkyM7ZaoWGotSAtsTR/u+2PAMtU4uQlSXKcYyl/LFLpLG3b2xsEhEW9IQfU/DAAXnBtySy3EzEjd4AFZEKTDfO92qI5y5BAM+YxYJMJmyBYs4PJCODGyBz3nJWt82aleQZstAFAMAOYqfeSwhgwO0CjPQUAYGcAX2aW4x6UmA/5vmFAcno2sucJGO0GALU35Y6b2Gicc5FRg7WcvAuVTKw2uh67GgUeEeBxfck9TeEMALR5mQBANs0gkG9l+a+8/HJ47aVnLXDypjoHVRVu3rwZpvOZiflPUFY2IqMDdgDMWIBr1XAeGYf4/mR8YJo7ZIaZ7qVE+rFOl+cnXDuYCwIAyeZdeYdeMclWYAGBcefBvE9AjRB1RNm0A+xaAw1hx3BsnBPNADDPlFjQsxT4BxCRIP7ISpRzCQ2+r0COXcPR0VwlNMkPsEC1yzK33zM7ZO9x3J2ByG7GzsSJibrI9HOgEJpmDnabvQVQZsE0XsmvEIOupGrvv9/ec21+lYHF0wAAMT4ALPACAMjn75qsYgJOMFeYyLD9I5YGaR9U6aU7+jiaAakZoN3S/TH7ZN3CIYCPJjLQZrWySjH6qa0LBlHmFrTs0BMy+C4rCb56hux/4mkDgLJD5ZkIpnjDGiYWHQBcnm8IAGI9C8Cx/dU0dPsAwF/84p8IKAAApFaYbNpwTOkPzDbYD9iOv/qrvwqD0TVjQE3G4ac//WkAA5YAP4AulynBNdZbY0TNZjs2KZhOnBF+YfvuYAdgAuvMgK/oE4uAIA3wgQGVAgBnEEElAJ8Y2JwnDhjik1wvvl+AqWfPxkr+lXfTdFHp6cjX8aSahPv374cHJ6fsArzawufYBnQjPTo+Yik0GFlsvHTjegAjR/sVJRc8oYTx4DhkzTJ4H35dYJDZPu17lBNRtmxmAADWnnxUbHIAcLE4tWqLGg0kmrBpjJEWv7cyQFOBdNPgc7Drpqs62qJiBSWzzrx0YDAC9Wp6Bq4wOsIPLNlKbWYAhbU9L0lb6KeAFACAHA9P4kCTUnGfBeQn1gV+a6XWo4Efz/3QVPgorWA1N7PxbAaqeEmlzz5SvifIcCPRBHTQgGMxm8bq0pppALb8jSIRV65DIww01IykH+uatHXtXbQhg4H1OfVS/F0dTk9PyZRXsy4mrIJ326aEEGwpJHSsgkoMe+6PG2vMB/1qVlaMp+H87DycLs4JNIL1/9JLL4VhY8A+AHI2A7o4T0knxLRkwmNtqNuuA+ZMHKVN8WkDgCn170/J/cLUNMMmuvxV2REBgM0OzH4IH9t8RJMuru9qwO7iL730nAOopnk49f0LACDXn4Asf65Dr4roAwBRko3Xcr0JN2/eCjfv3LSE6MBwD11nIlpo9rXvQ3b7SQDA3NbnzXFQOZF6TLTjr/w7JQC49ywJ7LnWf0G4wn2ZDXY/RVNCCUJ3nLqYxT7xzCxcAQBq/PPrbt3D5fjfHgCIUf9/yHvPJsmOJEnQg2ckKQqgCDgaaDbd07PkZFduZ3/n7cl9uD36X+5ud1a20dMMDU4KtCqrkgSPOFFVU3/+PCIyq9Ho2Tm5gECqKjPixXtOzM3U1NQI+lWYks+TLQCw6QJsw+sgQQNTTphLzFo3u68LzL4n8s998hSfr8EUbmCLjFcbc9/lawDw2tugo3UFAzAuYAp1uzOiKPPt125NljLw1XNpRQHR5581uGdmQRFcbr2nOPibe9gdgDuTbmCl/M7dYxQbOABAH+T1e58jfrh6CnIA3wT+18+t3kEAsMbF4nr7RTP3jY8PyppBo31QZ3byuqxLwCtD0gTyfqoKAN/DELxu3fr3GwMo9Tosung5m1xe08Ftty+qOIAOOLbHkQkHMIjXN08u0meffZZ+9/EX6cuvvkqnkH8AG6IrjQ2sXhwIKBWlvlIEhyh6xMsAsgN4MNt4cKAxQnTLNKAuBpuCbmpQQDg/Shl6QwSNUBgJphGyYut1GiNKZJraAAAgAElEQVRzHsAdvy9Kf0cUCAbYJ7FqNC9BCcFo2JNGDxiA6D62FlhoxgUcRY5NADtwcNoAUnsed4Iz5UAXoIgPlDJQc+lHZkLUmfxKMqG8tAKL6KIa993sa83ffDFXaSManERJjkE+ZExRyoRgBqCXuvzq+fXeHh0qii2PlGmUloTmiUADy6+is/KOBIWdN9yLQcBy7zj2bZpmhB0oNGi3QHYyN9obvym9s6i3mUlNiQPXR8X87QWQwlI0jHUAQ+gWh+caRJOELoPMHh1d68FRewXUhbL0z90XnbEvgFy8z8E/Ms36uzLqDjga5nowovL97klo2d5VTJ7M4gtA2SzQbDcKsDgHmzsYgP6dSoAbcFF7RACS/h7nWZSq+rxE6ZoAQzPWG803gj/OWMbaqUuVMgspAGgEqJIO0DzcGB+w9OXWoQKMcWfO4ADAOrrW3bhxIqA2mGpoQgyGEMv4EbgjAVDusRAhx/zguaxnBduB9y2mE2l9Rkkw/2RzDewzNJMRgMf91oUO4UEr4MYwiAmi+cTWZAC7UBdE6SBJp8isGnW9VKYb+8WlzBib+To0E6mp2uxdaIRltjA7fLqUy10sY/8W588uxhfZMMFO1NqRve2jSykZmHrOBsCOf7P5kRIqtPc9AScK7sReY0A911z6DJAt0XfQTu1WHuF5zMAyHPhdAKD2WzAeQ+OvPlfBHG/ZF/sjTL43TJ5lUsdONrVBkxA7GBHg0rYEAGhpgmavNWe+AwuBqghw5V/gbHM5IgHbHJiaaaV1RlmOSOIYKKyf6S8BAjlm18rY1N+4/e96Le33x/ZMcFxy3/la+sLUunKp6FpMu+nFnH7LaHSkLq4RyGL/kWnkwC/+7PfEkHr3N+8ToABjCvsPDBwzANmUp9tJ4/Eh9/uv3/11Op8NCFSA+ff3//7fp9FY16HeL+2UAPCLs1PakvEgpb/91a/SwTDWhBnkqAqIfeIzfFeDvGady344QdRIGXmt6fcoZS79v3VOeOm+8jkazFakDvAyYIDmDhjHp8/O0+tvvJE2A3RyhZZxYtOP2eIyvf/B+w3wwiZlK65n/OkmSIsAIlRJowQLXoMCoOA+sD0KPwgVMrJ9ssd5/7BkFz5nlAzb/w3g0IxDSGnQ/sAeUmtN/hyafrGCAppzuKdgGAFAxnx7vS5c4kjAvptWXdlhdg/udtLFxdOosJBW3XIp6QRXSly6+YS727MJFpj2en4kGtlFej0hgAbtRZ4HweBWV3doPyrR22jsK1EwPpSEDV5MwOMMKhjFjvsappXsoe9vnJRYMIO0lMSgn1Y04dJzW/NW58lwMNE5ieYwy0VaA8yMeBb7ZbrUOYPSaK637ipdTiassME54PMVTQJ5/TUALUlW0Qctuhn72fCc09lSmpoDdaWFliDYfWDEQQN4MTnneByPuwlSAJvppZJP1OLssYTafm5pL8vnbwNG4X8xvitjuHY85/MmE4eqCqS8fw18Z8NYAWaZQRZ+VSSMUGGF83HTDQ3y/pD+CCpToNH31o9e5f7sxL4YRgLS+znfV+ApnZA98X25WYQZgKgAwzygySMYm/deflHx32KWGYC0Q/Gc26XGeXm2/lKPQ23X6k81QGMk/NzkyJU2FRBwHfMv7xkzYCvy0XUA4O6naqQL/PvahtdNePaVMNfX33d+Ns1E20jM3q6/NQPwzwEAdVPtwEvciParBsl2DtYOAFCGo95Q8X1m34UO2a5rlhu5vs99E2YHNgNyXggtYLJpN1+Xku4CAMvvyhTMqllFCQC27zvscUzUrt+1r19DxDWAZQfAFPUmo371tXUdO4PPUwJcM5Q4n9dQWLfnaTdAlzdUzRLcowHYUHqvvp6vm5l9LS0MfDaC3i2kcc+K2vO8TZfQJojGFerAZd863TdX1wGAzfO1DQScFWmZTNV0A9hdr5uO+yNS6b9+9A1LP9778DNmeB9Pdd8XyNQhK9cfioo+VwYSDgYdrBCqh0ac2Cx1l+zQTEP2DsBcaPxlg4ymHdT500+mIX5vBhoAQJafhki9RbWHoz61vu7cOqaDjn2J54DjSLCAGWh11yNQGY7ibBEHewCTGZBzZjQ0Sp1Btt6cGS9lIFLOXf55sXH0M2vaxLtZ0gwQSIElAlLZmOgmF6VsKltwmaXXkEpoy73R6AUq8GYjFgAI80XOpMrOSovmyeNTZqCp84EuuBVgaSACJU5kJAEjy6AZ4lcFDNJ7C5ChYErDaSu/r6UzQvSyGX/aG2vO7CC8thi/OwBAjy8d/ihNyOs/PIh6H6JEQgzMcMCStN2Wc5WSH1Cvcs3SFnw/+1ZHN0as3eNKIyhF4O7vgai6ArAm0eR9osAkyjudFCKTqCkJrksRavvAlb0nYOd8/EAAoB0qOucG/4JBRiCPDJVorBOMYNyrS2lzJhWuYpSlc11UGaQaAMR78H4BpgB9tI4NAB4OBunv/u5X6QiMzMEgHWwgMn7OpivoMkg2AMqTrN1okeqe9gYYtGAMsxST962kowFAlBGbPcczYrVQMw0EeQs1/OC6i9Lb2WSSZrM57U1zXQRlwTRg6bxYv1hf6E7q/QHHHY1oqNVHgFPgHgJElSFzNDKIjN9PFxEIh4aowDYwPQQc+d6b5ljN+S9mVPtVryWsIZbI50DMgJ6bGVlWIkrvuwIyIKWgJEIE7gAA+4OcENKewL00eoCyI9J4VVKn0VCq171LAv8cANBrqbzWdQAgu76i/CxKRw0Azt2MIDRQM4iZmwRoYTcl5Po7GOwOPKXVpWe20L39QgPEIGh4z3HNxzng9YdxLl+1H7sPQKvHs3WNf6YAYPls+wBAMwBnl8stAFDAi570KgCQDMA0TOODg9QAQGAQQxtT5f9oePbb3/42LbsnSuZ3U/p3f//v0mCk83gymXLPjg7EuFrOJ/Rbjse99OOf/CQBt2HiZxYMwDwBzY70OinnxgBmPjfjgfYBgPYPG/8x/PoA5ksAkJ1uKwBwPBynjz/6OH393WPa0n40NQH1QU1B1ukPf/g9wYFXXnmFwCiSGVm7OfyCJdY9wPNMJY4EcSQmUYqocztK1sNOL1wCHQeF/Di8LwDQaEPqy6IUGC8DVSskGNgVFw28IO0S/gj1CjcEADl/SATgvLQmveOviHdhqXndbvheqAoISQmOrZtbrWXPfY5BY07/jnUHLcpogoKf9CH1wPtS05W0FKDWaPXpc2BiieGK5GvT1O3s/GlOFgF0hc+LpC98GI53MCt9DqNUWc3XNB99UwztJ9SMjqKLsRMimdUEoDGBgYndgtL3ZWYknZ9LSqV/cKTzik3z+qk7kN/ZdGNWc6tOP7S4DQDySN2kVZTIWwubrL31Jk0AcoWG8gKAdAKDXnItt27fShdPnzBh8sIdlLB/m7qLKRNvrAiCfi6ZhfC1Bax6f3jc5ZuVtvX5AMAcz5rhfg0AWHf73qo0qio9UGFlANAl+IixEP88ePAwvfHGy0oQrhWX1QDgfgag7aIWqgFA4Ln4HmiPjg/H6ZXXHnBvX04utJ/cZOSfOQC477zbJUGBtVD73TVguff8rCXEKjzgKgBwXyyJ76qlxfz9vs+6dL32q8t4tPQJOv/Tf5QGoNvHOzBuqNiBuFbAX9kFpS6LfC4A8AoPpAQBGbSzJKedmdwFLH0fABCZiHJAGg2kdg26gYTWYRylGrsfpQ2g4T27ADJk6h2M50mlCHzDCrxiqDL1v3nPbgDQjrYPfDuV+6+9HwAsy4Hh0JbP5utl4PAaAHAn+GnB2eLwybUK12rl6Q5smKwHt6+pSr7f7JgU4Ap/+fwAYEs7a8fAboHMOwDSJgBrZ4T2AYB2NLa+boc2pIPVcr1BywEH8Lh/II2bs0X6+OOP0h9/+yGBv2+eXopyPxin8cE4XVI/bp0WkTG3A1t/vxk/KKn0WtO+btZnqdGETCVZIcEcdAkXxNdpn7wmghV8MIBOyiDdvXXI7PPNEzWtcGmLSn/V/bI0oBuWLKsLMcCyVZRk4D0Ep1zCVnWD6lBjEFlL63GZ8dQwGDIYUKzRbnS58vzV8wiHDuMvfRQwdcwugrYLgm8BhIkdh4uyOWcQi+vDAUM3W3V2jfVT6PFIHwxC4NJ5xD5GCQ8ADczx5eUkrZf6PtwnGQxguY3HFGyGw4/ASYBEMFfQhdH6h7sYgFVpssqskzoPzxcMqpo9GEDZc4Htds68X5tAWfswMv8EVyNYiNKn0t6PWGKk8eLPIbg+n6f5bBJamGFPjEjHc2PtAXC6MVAp7wDlkGRkxPrOJevx+YIJSBYUz7XtbqlmoyPAceMLXXe3pUapjF67Ex0ZsAtmla9S23+vy6wRYwZ6BITQ4CzXsNlm2f5QK0lZdUsC4Lsc+NgeNEwE3cm+TKXfh/sxQ5fXi/UkBlkvHR8M0i9/8Yt01NuQbbeZaR0Pu+rW+cLdF+i4Do/GodsHYAyBkth23d6I2oHD8YGAVJcGlYFPzCX212w2YYBrZgJLzMtSv4USD8jMYn5nC+0XPAfWOppNUCMVbUap/dQA+OgujN8hcFkGcwLBCgNzE2PZ3RGltEPpdUYpqG06xpPrq9r3LkmzjTKwVNrWq+bD+0Pi8kpQqHGN/CfbJu8t2En8bDQaqNSwCzYQwGow2WR78PvxgTQXc5fhXDIrrbDGtzRwaX9U66cBACtGcBXIbgomSclOy82cvG+rxG8TGEb39dCvtSTFeZREo2kKx8+7MRirsNuYT7J7QjuUwGjMY1oJAHJpmO8z56GZcEGw6i6bSpDZBytLygxClnv8zwEAdyUSrkvg7ks+PD8D8Ooap1awYl+zYC9j/ZEB6H24gqTJIJUlwATRo8lDDQBa6w6lhHgfGIAAAFfrQToYQ+svzvuw71h9sCfT+Sx98MGH6ek5mOFsnZH+9b/+79L4OJhO7EbcVPegOyee5cahmDo3Tsb0Xzbzx0pQRCBNkCe0mkp/Ipf4Flq5DFQ7kjDJ0iUGsiJeQt6ADOWltM/g42EfQmOUTTvCP8Jzca3H9YcG5lOihtrp2TlLgDsHB6ooGPTT0dEhGbzvv/9+Ojk+Ti/du0fNU1wHLFXYQeqsthLdrrTRzz3+3r05cZ+B2uZcp62K5QJGH8+DKPVstADDH4gSaPuTtBXRbZX7LQBAdP/ldc0IjQQG/FEBg0oMAYjkuRbvQ8a87MoOLVk+ZxBiUKLN7wkGnSUZBEwWJdkbSI2g+UP4qetlMLwDOAygjtcqyDFZQmYQ0gDRwV52tZcG1Kgd8Fo6P5rzm88V39ePply8fjTGKuODumu6bUsGyhYCJlEpgX3oY+f0iXT6LmYComDXWEnS3UQCP/znuI/ZCqXlkMAYCOBaR+IVMn1MJKn5CuYT5/DTZxdMoKGyB+txHk0CscahVTe/PKPdPDlI6cnjJ6mzCk1CgJ5ovEf26CYIC2oO1PLNI+FoQK/0v1iC6nUYc1JXSuaKsVrb2ANYlZbW18s23OvHTf8sl2RgOgA6JILeeuutdO/hCxyvxUwlz+Mo/W20/vIM6vzM9xfnajhkBrzNALyYzpjAe/WNhyzdPr94Sn9pFJqSPqdrpl5+3D1/qf3Bkg/ORGz4AHXljhkimRG7xQBsmKjX3UP5+21AcAcTQQu6fVknlqvjrDmn9faa6WgGX7bfRaxbfkHTtLDqJbFPIig+vBVv5kraALR/CACQBrSYgL8UACwfPDPu4ofXAUvNA++ZuGo1gPpddpFsmo00n9c1Y8CqDq372oPvErGvxdV18jQAIA/+uH5Gdkt9qcIB8mNsacBXgWCDKrcZgNnA7NTd01HG2ytEJXfpADKjvQfkKwUo923CqwBAf7+ZLNffc/Mtfw4A2AKwqyY2zwsAbo1BJaSvPWLQKMoGrwJH3QXNpRNF9zAa7rjW8wKApWErSwT6A3V9BSEOgN8nf/yYju2zxzhAumnVl8N3Fsy/CYCPXi9dbjosGz0qtpkdB7Ga5EAtlnII6YAgUDazLqZKwQ3MfpREWKw7DBW6zqGcBtdCQA+gBcH/nVs30p27d9OwK0Pf70rLsD8I1sxszmA6a3DF9yFgNNOO99VziV7Mjx2xwtmSQy4AsH6hpHnX3Hqv9Ko29zw7yj1n1lxucKD7broaRpfNYOE0QZ6a0vj6ZGPCqVm4yYpEzpvS2GggADZg0P5Vlt3j952fn9Fhm0+sQSNmCr4PwArElfEn8Fk2HdloHFEiVDqMzR41MKfyYF/LwARBEHaDk1akx1CAwvPY7jYAiPVjAEqAjK7J7LLL7bhv2vN1AKYlGY0qIzMAuAADED9fae31DUYEAAjAAgDMcV/fMRqA6QH3NKf6+f3O8DrR5FLzrAEW5ZMZ4HbTkHD46vOgXn9gkGn8tgHAEuTbVQJcr0XOY+EIl/sVAKDfn8EmliY5AeRSYDkomckXGlQlALiLAVgDFXYMc5e+bnRhNfsvGlHcOh6nn/3sp+mws6Z9WFw8EyDAjuErrtu7d+6m49s3xdyE9tAhmtus2T0QXbmpCYpGIezY7YoDBexs/IBuvxGYo2kEwFuIkwsEFROQjURghyyyHoGXS62coT2bXKTziwuWICFInk8KhqybFxHNaPaA1nW5RwACDiIx0DAIOT+utLYfEcCXHXqvH3xVG2DTb2pGuucKpddY30Pvp06UG5M5LTA7lwFzDavECwwo2k90aaeNE5vRWnaQaMDPNTdg6bjLZLuypHZTvNqfFwD0+zOzMG+kKuG3xSwORkSh6cb1Hfvt0kAFGekKJJlACWbg4ECabbQFkL0AAx1rk8AwgGJJL2C/SHuyrT0EDUtqlDnREx3hVUrdpTi7z1cn1ErgyN/9PEDgD1H6m5+10iC8SpN561B9jh80fm2cLWEfAGiVAGC/J2aRu3XvAwD7wwOO46/f/VMGANkUbDDUOYCSVlQ0dFI6OjwioP/dd9+l1D3h3j+/vEhvvPkmGU5gJBHU6eOMGPD3hwcAxFZp1AuplUOV+N9/YcQzEEBGOW8A2uFn2Y+wdIq1ujLjbQVNOVUQ8AwNDT7Lq8xZKtnoTWLt8Vykhm2vAQBDqy8zADdip8O2AQA8v5ywC2h3PKbfB01VAIDwHx49+pK+182bN1VpAT24pZpD6OxtNKlyUxKXAAejLovZm3kX+4oAWpZlsURJA6CZsNI0c4g4zRI01k50qaATowbEYkHYjjjRkUtnVwKewADGswACJKkgmra49Hyzlp0DU4/70dI8pYRSlP46Qcl52EhDe7OW5vAAJQOUhlBpcG23vTWaSgYAzdIlbljajR3nWR0Jg5KAYk3iYZTe8uyIcS79hrL5l7+bmrUxP/2NSneHTGCjeaD86Ivzy3RxeUnNR7xgL7EXoDWOdQd7QNmDocZrukySopnDjq4SilGw346PpXHbCyY5zkEk+L57csp9hnOYCWyAgPMF9xSB6pV8y9XsKQFAayx21muOVbaHAbCVfg/3oYESV6hUNumHAAD9Pdy3TvDVmhchVbIqAEB8LkusLDbBrE/pnbffTnfv3eZ5MZ+e87w5iMoDNwHJBKcgOlwHAJp5OiW7sp/uv/wifSoAgLAxBgD5DPADDIzuSUjXpr0EABm/7PH9XdFjjVLn9/Ylxm0nd+EL5T1s/772o68GAJvPB16y5+zKicGcmAw7lWPK3Qn8fH5nRnJbMsNNVPcdmbsAQM1V+Fn/4/8iBmAWVYwMR80A9Bd4wrKj6Q2ST6TnCd6e44SPt1zVBXjXVb4vAFhei0wfazbkgKgNAJYOyPM/jd6Zg70wNAre24HproVdA0i81tYbtxmA/NI8Tw3jhj/+MwBAvL8GATFOGeWun6Eo9do3RnvbeNuBrNrT19ep2aL1htEzFmOyQ7PyLwEAd4Gf7TEq2UfN3WfHLmNK7XnLGVGfDFnjrWDQsTSp5uzHdxSllQxMEjpAqlsuXjiECXgs0OhhmD777Iv0n//Tf05fPvpGGnmDsXRsOkN2w4MWHxy/czomy7SMAG+zQIfLpkMyliMyZF5XpVOR2ZjVOlKpcKz/zHRRUDiZT9Kd27fTg4cPWdI37qNsb8mSZTg+i9VF6CdFeWxou/hA2mTtK1/Ymn66x+VaHSm9L2tAH45pCXjUXacgqt16VeurTIbs3ANgnC1U1mgmAcYXDDuBZdu2oVyv9fZtnDw9r82DbYyzwdDvYoABkHe5StPLGbWPLi8u6Ei5uQfZbSitPDjg/9AaYQkju8ZF58LQANReM8gdGcXCqdTzB4sROlrRzMFjr0RMM0q7Atld0g7l+6B1VjqwBiY8ZmbY+VsOggnpZgYIIMnCWkgsHH/q1TBJ8XMDyydg0PYFImE8KXMDRkewxqh2FMwDrTP923vGgG4uBS70aLlP665xXsa5e+62BEe5zkpwFo57/bva/mcGYNgJazhm5kMECl5PLhUhqcGdHXcAgJ63WgRbmjvbPoMBFjCtOF6wXcH+U3At7c4bJ6P085//LI0RKmFMJtD6geMv5jE0jG7dupUOj28owO6sae/QBIgB06bPQB3OLUp2yxcc6NVmlSbTKUuRABBKP1QMAjABp+sZGbWLhYB1aE5RniAY/NAWEyCauK8up2Ieso8fyo9W7QoEBoED2KRglBWsDN+bmRDch0WnW22vyh6FJEPzXMGkM1G1Nl/VfDRnp9h4Gcg2wEiGBp45gPYI+AGEEyQf6GxAcwPOR0gvcPwoD6B5cIMQMKabv+Ned/uTDQAYdmbHc5RzWTMN/DswUgygyQ5pDRjw9vtsU/ynWc+hIEGmGJtLRJMFMFKwXrtoqhUBJ8CU2VQC9pOLC56nk8dTrq/LyUwaWtQC7Cc0vwKQdevOHUpbHJ+cBINVTaxUDi9mKc/3CBxLVj3utQT+Wn8vqyt2HEx/KRj4vAzAnWfinh/u8lVxXuezE/at0gCEmh0ZPx1JlwB45zkVwBOaOGBc0OwKLwCA6AK82AQDcKXx7R6oGRbsFYCM6WLO9x2Mb3NfPzt/mu4/uE8NQJQ/9oZq9oD9j/N92B+FtjFKg8FCEiBysFEJKDSLyexNSiQMu5Y5aNY/A//oxg3iANfrGs0SsGmjqzGa8hA8VELq7u27XC+wWdROm16KsTVU1/OS/CD7rf1gv+FgNCSAcjG9SMfHRymNBkpQsplPL/U7B+n87JwMTHVG13NAYw8MX59v2XZVYJQrADKzz/mz2M/QwON8RRymyqhGqxj2nK9sJ8Xs6qK0t5CLKtIpOgfSjO8brAXQE9IPyQwNbDAMqb2npko8gzqhIRjdhqGdrQuIAYgSTX48uu4uo5sx5qdcv5ZkANBLO4h1CIZmMBAtn7NEMiEqGvR5PW9mdGUGSFSs1U0seiE5kLUSo+Y3zncAlaWv3jQ9aAOpdeLefmZvrXXVxQiuVhnAPDuXrvThsUrkMX44O9dzNN5CGfKC+3KOJDKAug6kapZNN+CVznv4ZWyOwm7yAgvhp377+JTvHw+OZEfns9yNGqXoKPVmRdPlk/T4u+/S8vJc5ekol429yWVT+e9b8I/3mWOEwr9nFYcZqLHAt7TsCuY5v684z2iPzRx37Jy17WJZM4nfSJChZwCB6Ph5jut73YQuwGgCwnW4vJAdicoW+KP0fXNTCpfchz/uRJa1mM1ITn35K5C/GQzSCy/d5Vk0nZ5zfw/MeK0kdzKeVHEmrqr4sK/B5w2f0GeWugAX55kJMqHtyi1Y4A+5hN4AW12aG++1nasTnwYQc/MkG7BsyILEE//eE4XnT9XUkcyg3dIA2w0EZmZ0RaDZBwDa3tWEti0NyB8KAKTR2yHOXo/b9/23xedzIFdlGH3dGgCsNQXr7/dAbbMWQ6uoAgD1nKH7w4tF6dieB6uBPX/eCxuBgMupfAn8rqG2Ng1K6q+QQ10vmKsBwJrJ+OcCgOU9iElmJkR7ifMZoqzxqjnPQHP1pgx2VgteG/1qtJxjXBikf84AIAyygYFyCP4aAGAcKZwX61P102F69ze/Sb/+r++mZ8/O0uHhjQDWtMbRJAMO7CWCWzhUwz4P2ksc5MjkRQlFXrvVPDpoFxBRsIjCYFPnA11+lyGCHRovFjp/+dWHzDwjCGJXuflMbJPVhlon3Z4YJejqiRcyqHlvUSTegHcAaRGAet1BvaQEAFlKUjTgcYlMdpJCNDY/ZtGsgj+z6LS7p12ngbla8YCFwwdGGUrG1GVLTSegebZr36tRCvTq9HuCIm7AgiAySifKph5m83hOCEJ1xcDDY11eXmYQEAwVlcZGdz6UiR8cpMMjdVWFgSqB/8aJbGyjtQJtL5Xpbo5K7MvVuumiXgKADBzAiLEIdx6EbUDAQQaD87Uy0a29lPXGQnepODuGLgUKxxuahljf62WIi3NtgYHVdAvjv8m8WScDgAjWyUCN7r6QBpTmV2iwWkpgIyaUAT+zM6ztVbIh8Qy7AMAyODfToAzuPWcegzxPoa1WAoGtoKRgAJqBagAQDlF20ApQPIvrIyaLRJYAL82zuuli38e8RYCU722P51QCgAwkA1CxE0iQhOXp3fSrv/u71F9MOE/9xZwlKtOzZwLrhgj8N2l4ME4nJ8fs0omAAtIH7PK50v0JAAxBdDZlgKZgP03n6jKOQI1db+OlYHmVJktpCwH4pnZkdJ1Eia40hBDgLdPlVIxXlOIS6I/mMqOBgAXsxTpAxP52Ob73OP6kZiWBxk6ak11noXtrJWpQFbDW1iPmYU9p0i7tGJ9P8u8ENBCURXATgAOAswxiU7wde00JDACasNNkHAbwwudgWSICmqZkmaYvtEvdvX0XcPXXAADlWzYBkbUIvV/K9e09NQ/7bzOFgBKs9kuAfbNZevz0Me06yrsRuJ4/PU2Xk8u0IBA8T4PVkCVvYPoQcA7WNhJsWLdYv0eHh0zCYD2++OLd9Oabb7LDNe1dlAjjDM1nsBvnBWt5Fwh4XSVNvtb31AP8oQHA3eCfEioGAPmezYoJSzQB+eKLL06MvQUAACAASURBVNJqpY7LiUynTVrEOBkA7C7nknAYqolQBgCTugD3Opi7edr0gmkLAHA4SACmcF6uNwdiIM0uyTw6uX1CBqG7Nrvp3qA75D6YLyb8c7WYcj5PcGs9aEGGVvFGFRMuWbTUhtcbAEDad1ckreW/wJ6ZkcxzIwBAJBjKseOWpS3TDsKzN/5Pc7bibzxXAVbSL5F/fwHpDvhdQ9nRXhrRX8IQ4/3j8Ujd1vtd+mz0aYL9ugtUzqzhaFbAxFowBmkjgnmH5nK0gZlhpHFwYgqfszwEnx9duPcAgDqXlODrLjWvTvyaaemS/jlkkdHkLhrwbAjkrdOaAGA3Db3XVoNgCnqCVBmxyYFcsz915wL28oi7KQUlYMJPRnI4VwDZkLcBwA472ut8FaAkbdUGTAntWZcuh/+LihZKzJixXmjfG2DiOmuQivog4b/760FaLOZsooLXIM6xx6dntHcnN26pm/lmnQ4PDwkA8vrLhRJdXenCogQY+reXs0hcd82cBgAoMFmJMTAAL8kAxHP3O8MMAIIRiD378isvs6SakgubKZuAzJ49pd2FjwD74LVRAoB67vZj1hU8ubmhk5YuLY6PZeZg3T02G9QqYVuV8HkcsWxKICwTrwIA9PrE/SEB2R300v3799Obb74qv32jLsAD+NfwZQx4+T5jXTYMQP0i2xkTSzoDjjeeC4lTdAFG85/5XE1V+uiqFkSDVhVokTAvR/T7AoA1UcjEJ2h4cp1WTAhXXGSCzhZOortqANH2vBsANBGspY2OfeNxjD+/LwCI8W/fe0HwKXCmvwYAyOffBwCWouga4TZS3ABJGoE8/pl55AFtHkjaB94AlWaSAcQcaGyDPMx6xsbat5CaaWwOs5ol1gqWttrMFYcgGVMRyNTNSSpRbH+vGFDbpYI5M1+Uu5ULtwQBDQDWQV17iXrlVYDfHsBhH9DnQLv8rv2goDRuysDfBuTKMb6SaVg/VTxPtWGbp2wON3zSmbCdY1M4Svt+f11pih1Ff75m/Hl97Lt+7QiXRlaGc8c6L7tSVt2ga42CYTDiwKBzSaeA4WVkrNfSFCGYskroykk9tyEyZ5P0m99/lf7xH/8xnT0+leMWJbag7yPwXVBcd5HmATC5K5uZG2YW0pgUBstrBKwoXGvUA2tB4MzF+QWdXwfzYKAhw4jM8fHhUbp/71564c5tOgbjoT4HIIXlwmTHCUjimu01YI9AxihxydpU2MPF6gkggffbRQCt0mGOzXKZMJ6YYwTtDNTNmkKXTevzFKXc0HIqS6qt9SKnXALDGpv2ujXACQDPmnx6X+Owe0/SkYzn8joDcKbGG2be6PrOvAE4AkBrsC9b4+qkQqbNTjqeHxo+yKheXkxYIglNwvLe4WDdvHkrjQ8jc76G6LdLnlBooOYDYESdn53RaeA8m8HaajLCgpoYHzcFMHBR2O+8B1R60nI0dpha67uUyRcGgaHJoy+MLnrhaGWbZza2M6UL3R+YgHBEvc4ZQsxm6RjaZmAAsgQHWo5YxyjlQdlKL6FJhedPf9rBFeiBklGDKVwzkVCSXRaAIjuxx8JkVl/Dwi3fmR2YsNsGM5rSovJ8jqwmmLEba1Ca0alxcPe8rEO2VgmQny13CSwAQN1POGpxczlAySXyu5M6LlN1ebfBXgZfwwEZgD/5yU/ScKPAdMyS7nU6ADt6sUw9Bhnd1B0Myay6//I9lvAhYMe67fUOuPfB5MX1mjUTTJgig0/mMzQ2UZoVIuJgbPn5CMpbLH6p/Yj9DYYQuwSTKdF+zv7oRKMTiQt/vzO3zfvV/a+PTujBVFTXSWlKmSnZAZMITIkobcpNAwIQdPdp22cwiGwLuT/iaXIgVGiOaRYbe4a5gP1rsWoDUAAASCZsMCwg0UB75gA0d+0UU8nzakaSgAnreEqb0e8p79clZtu7oz3OWxpP8QFYrFLs3Ta3FIP32vY6Z6f6aE7Q6fcYcIHpQ7Z7TyVZWIsAgt7/4GOes8+eXqbHT56kXv+IawEjyWfEOimbI4T/jKYJtDuUJQXdHSy1IXq7EFh5++23qPk0HCrhYkYumNs6VzfSmAt2ts8oILNYH1y/q3byepffZ43nPdanVarOADoDvGLubr2qCLu2a7LvRZIouvY6IPb1SsaY/I5oRtVBl+9hmk1X1DKG68NxSP3Gb6WgYgAWca4PDw555n/wyVfpy0dfMvGJEtdlJAjWkKpASex6lQ6Pjngu/ulPf0rn55KAwP6FFunxrWOy6AnwFwx5zDW0il986Ta7lI5HsjfDtQJ1AJe0Kwsxpy4uz6ORgzXwPM9Nkwkx4YIZHdIUrlCwTwFgXnFXIxnCklafx1WCTX4LUEL4P700GKo00+cQ/EfbO/wcCVdrHTKJEUm77qAvu4pKESYM1NzM3UjNSCIIBX+3yus1TEeVa1pSBoA49ld/ID1DAKtsitEV8xGJT6xj4Jv4nLV5s9aWz8uwQ/2O/JhlV1qcq37sYwONYFjiXA+tXQCy+L5FRwCim5L0krvY+rwPiQRoBOP+o1nPAv7nWl2C8Rr5XHTM6IqJlRjTkE7Aupgt1VXaGoMesOOhmvHxv2C8qxRTCTsAy/h5J9YHi4pRoh37tD/X89s+ZOBko3UFYMmN5eyvl3a4G8Al1hdsHuYG3/vd48dM8BwdHKvZFZM66gLvF87nzkgJoF7/gDI007k0AhcLafUpMdLlvtCZ3+P+ePb0SZpOpqnXv0kA/unZaTBbe+nBgwfp+BDrYp2g8UzG4KNHHL/p9EIJ7GDGl/42t2HRpdqxMc89M94zMBb2I6oSzFgzLoHu4fpctbCzxFBIcGSg06NSn1uhx521/wyoK+GWen2WQmN/3rhxkn7xy5+pQicSogdhKI265JjCJdxUvVHivHxZMxjbHfP+9PKCfu6P3nmb/hT3fh8tGe2fimHoUt29/uregyQqx0y4yhWL7fvKwJvjoaIJou2S/mzIJjwf7McUuAKZ2BXj+Irby79qSblVHyh/x/1SJHbLeAQf0z6yhIvj2D0ErtwVuiZaBRP8mp4RDV4Wfvh1JcA/NACYAaIrAECOZeVwluP7lwKANXuQE7QFwNQMkwA+rwAAPZnc8H8BAFg+q0vMrl2QZYnsFWyj5wUAt5D0rS42bbR9F7BelqHQqBaAqAO6/c/1zxsA9H17o18FAF4H/mm9XA8Atsev1gBQKWyjWRIMEQdaPTVcgJFHADw+UjOHLz7/Jr333h/Tex+epidPTtM4AIz5FHpXYEEteIDP6JguCQASEOtJhNzdR8suhC1trwK4FKDdDW27hsaNg5wg33ic7r30YnrxxRfSCOXGKPFNCGD6Yv6w+5mYYhbfB27PDpfWkMkaeRZLUCCUtfTi/t2F0Y7nLEqGqGsSjBs4Ptb0UjkfRH+bA6Xep7mcOzKIpaMEp6e0D2Vwg30ynaqEsOny2RZ59dzXGo74N/dSlJponzUAYMmA9e/kkUQmPRwaBAIq0ZPWDiGnFcoe5yxVOz19nAWTxRxVOfDR0VhNF9ZNxloPKkYCgJLJRE61zHp0f/P0WBMymAUaI92bmEtxUFWAYROYx3vrEoMQdy7tDtdfsABzgBmGaxQBUD6co8TIdnzjLsdLaTPCGRZIIQd/sFAmFOsWDuugLyAe/WgACA7dQbUjxiYbIIRDTIe2HwBM3B/ihMwGLMS6oR6281UAgDt/v0PygGNjbZ4IDAzeZyAvgFlrRtmPlYaMdRPR0KEoIQrmn8ayXbpSA4C+10bz5WoAkKBulMlrGfcYAN6/dyu99tprabDR/EDzxgAg9i6YB0iAQEsU9utH77zFr14sFVCgRJDAGrsIy01GYLJcibGHBiF0bEPOAo6yNewwn5OFSrty/EYmBkAdJQ/Ozy8552zQAxuMphGRZMF3uQkRbsVzgJ+XDDcH8GCLMuGAUsYQ9sf3cDxQ0o+GOhbdt+5q7Hefu3BICdSEJ93bKAAstX75/fa0rwEAHQAbBFQTkp4YgASxtUHByNC/dX7ZnnZ6shd577McWGdN+XOfb+WZyufImoE12NReT7u0rHAffy4AaLsCgBbzaUaZgS8G2NC+nS/SxcVF+vzRV+xGOw1t1clMgAqYG1ivww605wDgGjBSQsfAsoGQzkBdrtGECef4rVs30t27d9Pf/OInPD/JBoXmWjSfwPplsxqyZorkU2gH2t7v8gvLM2qP1WlMTWFfagDQtjzvdZaWtq+4CwD0+3E9z9sPDQCyDypAiWi6cDA+4p766LNv06MvvkizVY9aoWAQcj8EAIh5RynsfDFNf/zje+n8Qv4XAEB0IwdDBglTJlzpU2zSeCyWIBJqR0fD9Pobb6QH92/r+1fq+mrtPtgHljq68iUYIuvYkOuNAUD51QDA+MoJQvspjQVhQjNKmZEohn81h01aLtP502c5eeO1LYMgxtpyJYASzY8AoKBkk+uqr8ReWkmqRGWakBTR7cAewU4iPIX/1NlI5iQtxaCDRis+n8sxww/w2ltT4xWgkpNSAYC6zI/ao2AoinGrEmg1t8Dz2q0YRoIGtyU/VPeX7VA0YVr2p5Ju6CuZaebmsHfIfYeEOSogoDHN5+xFRQyeA83VomCg2xsLgFlCx25Bxjk714Y9tGQPzj36U6xat6+qbvIc1yCmrFbTAEgjEZeb1oXmaug5w65TMibIA9biQ6ktnzekTpCiwssA4Ggtjb1MWrE0QE7gSfPZ8btZ8GzYMZ+nUTSnA7SLn6nBzCp988039B/v3LzDsw9+MJh3WlNaWxjvhUs5++N0cXGeAUAktjDOs4WIAdBihn8FLUDYz7OnTyidMOjf4r9Pz0813v1uevjgQTo5Rnl7Nw1Wi3T65DR9++Uj2esFGn912fxDrwpwCQDQAJ7PxasAQI6nmZqRwDBR6SoAkPstSmjjZrbvhySCTdZSBACsc1GNA8EExPPDH0Vi6JcBAK6XkkAZmhBRscENCFkTexcAiH2L+BZr42xyyXl46+0f8fyRfjdO/6J79z8hANg0PaolcHy+VPFa3vj6ufXcvw8AyHmzdnszcfxbDQBuSbJU7zcA2KzD7w8A8vv3SLo9NwCYtY7cFSmXKMZ2yX6WBhrbqFXik6mv0oxx6VTz3A1FuhyL7AjsAQAz8yMciO/DALwaALza1cldfaoJzOPlrOU+EM6srooBWE5ayRx8XgBwVxvrrVu8YmE4y5EzA4WeFzNFOxZUuciuAwAbAGN3gLd9r+335cAzZ8rav9/HAMzrY08A7O/dxwDMn99DHW4AwG1qzs7Mt4+bajx3ZdhrliHHcE8GgMK70czBByu/ygAgMyrr1O0P2c0Vml1ffvVV+sPvPkgffvhROpvqUD4ajJipuzy7aJopQJx3taBzN49utKae6xDqFAepHrB06HlwrRe8PwfA7HobQQjZZLdvp7sv3E03mG1fsjSADnXcNzKucCgg3ivABAwXlMbK2CHAJPMmylVZkh9NERhw96Mcrsg8EsCkZswmreAkMJMlzSYdDgBrgpmG94UWibOXXtN01EOUnc+Of4d9zAzFYoFb068MyARkNiWmtgfNvtH6MrOx+b1LlUN0OlP8o1NxtSzz/o7726d1hSwf3usmHc9OnzLjCrYUAAgH5uPxMI3HhwndDckogYwN5i+An9UymKfx/AYAPRwK6NZ0jEuWJMdxBwDoktC8bw0WFs/p65R21GCsAcBGLDzWhRsv+DroZlt0eUNJMMecZeoLZqIRcMCpZ5fe+RkdSpS+YB0h406tOLIRUNqlgA5MCe4HgL1Yz+E4DaANFbpIAlgxnnLueL4GEKJS4jaLlOdZAQDuDObdVbuyOwZUUDpP4Mli2GTXNvamTAxLz1NAHBiCGOd1AQDifpumRJG5tibkPi23a2onyoRR+XfcM+zHO2+/ynJIAIAE7KJZDzg/CC4JrGw26Xx6SebOyy8/oKbo2flT/X65Ucl3rAPMt8rr0dxjye/IpfK0ee7sqXH45vRJZkByHDvqsolpIWODAV5jT7DuWRoe8ce6K4az/QgASZkVg3UXVBEARtSSW2/E0L2cMoCHnUFJ4vBATQXQbZFdFIM5ZY2hbIZiPZkJ2LNEQqU1acZcBiWzaHjBACw6olNUPvs2wW4jc7thZPNYKhhisplgDBvwgw+mz3LdV3qYtIPxILbFHQjL+2eFlqt/VvtHdYmXxdGdGOE97rxOWyvQew3+ERn21GoVE5MdkUPK4euvvk2ff/5F+uCjTwkAASBwh1R813Km9WFGUFPKF020QnssDZFc6JNhSFZ3D93ae+mVN+6nH7/zTnrw8D7HCwxu2GoE+up2qVLFBvDTOWpGkhUtStCv/Hse2+Ica/31GgCw/lhd8pS7324x09uf3AcANt1zBSpgfrGv57N1+uSTT5iIajMA3RE1pBiiIc1wfMj9/MWXp+nTTz9Ns0WXXYAXrDQAYGDtrUQ/ajafEACcTCJu6HXTL3/xy/TSvTu0Hdh/SKLihfMA2nt4HR8NEjTKXnnlAX/fmX5VsI6RaBBTjs8S4KEOIFdgiTEKyQGsN2hQZgYtbbgB9mb8yPAJoMFMOtgNrGEz0Lzut+KwjkqS0eiDWoWjEdfXcOCu6QKyc7Ikml4YBLu4PBPACN8AiS8CgPi3dvJspYqLFZrhoOS1YJTxnlBizMSSEh8ovZbki+zvsCemFwAzjVu71HYdG6thjAZbHbUtAKSjRHoVzeRQ2suxd+XG0okI7Mcmnp3H9xzb32Q3XZQGDtTIyPGe7XzYT5eI2o8eJyGHuckTZWubLr2DAzEewaiktmLXCWX9KVCsR01GXifO6Lxv1zrfzdz284ERRVJABw2xCh+s8KfwvGC6lqw9X9cNm4I4mZYbSdkAIARwd345VRO+qKIBc3M0hK+zCUapNCMNxMFvvLi8SJOp1geKLcgMDIkafC+lf3qdNLm8TOfPTnkO9gc3+D1Pnj3leQ07DC3OO7eOCVQB4D89fZK+/OwzzavjhCyRY43JJv7XOY6O9eEj7jgPzHzrVwnYJnF2tTZzJjplQlSbje3vtj+dgeMAADkujL8GeTyPT46ZEKLdj8Q8nh8vd9u2rSgBQO7H6mAE3mEAEInO8+mE6+/1t94Uw3omKSb4pbx+SDdlBuBzNgFprNTzMgArgMzNdsoLxd9b/vAPDADiLpiAgSdfEpxMfLmuJtj3G/Pf9HLYDwBaC7R81Oyr7KyEUpKE81vhdgaot0qAdwGAPhy4gaoLNQwmbyAfVH6QGvj5bwsAlkCXLEKI5+5jWHgx7e1MWQFWVUBq0NEHAsbv+ZhhO1b0zh89H7C2DxnOC2iPKHTO6IUR3Aqud3x9fubn1OtrP1Z1QR6cKv3OLNLiA/+tAEAftrvKvZ9nfv0IfykA2OicqBQ1v7LY+jIdjg8TutyhFOmjT75Iv/vd79I3Xz/jWzvDuzpAFqF9tlCJgMpgFwQAmTmLkq6ySxMzppWhs0Pu9QYmAsV4o0wUASyYY+PDw/TC3bvp5h1lwqG5BocYM833rld0wLNDiy6MRckFAkd1eFVgZsDO78+fi5IQ2zC+FyXH4Qi4hGyXvcbzWXQa1/NeUWAPJo8AQD8rf8/Mv8DFkkmo71dJZ6mFM4hMeslUlEPWlItqXpuMFoEzl85UGhK5hNTUdjejcNly1d0ssUxYGWg5WQAnpFvmkuCL8/N0GWw+CqFjHnoCYA5GEqVHgAItGHIQUSKD5jAGG6IEuLXPrSO3A2A3AFgC4QIkGtuQGUMFo5Dm3M9R2asaAPS9dCMbnec/Ai37C55fjCuCjuV8KoYHOwx3U2dxwfkEYxXzTw1AiiPr3ygFZva7L8AQK4BAVjxLryvtoGwPEDvA0XUJEthUEFxncr/tbNDO5ISTxcIbNhfXHO4s1lJ5BjgQBMBeAoAqbxWgQccfXei4xgXo+u/WSFmv2o7uDwEA1na+PE/s0BgA/OUv3qG2EABAME6h6cjulghSh0OKuZ+dn6Xh4TgdjMcsncS4ITGhEj2VFMEGkuE5D63HKGUzMJhLerou8VLS4Xw2zXZBJTnqysnqPO49B+xiwCKRwXmxu9RXAJYBwELzls+6Cg29CPxW8wWfczqZs+QK84vnHB2OI6AC2AMGkdaZ56MJKFSyCACQIJsBnAoA3GLM5a70WpgGlZwIMqBZMvkUeGp9OVDIzQpyKbGArgYEdIlT0VW4LAmtE6gRCNm+10FbmTihXc0JEP3FAGDz81ITrTkVyoRMuT4B+IktrnMAWmEMjIaDzMzH+nr3H/+Q3nvvvXT6dEoAyWL0kzOdr+W+wXj439AC5Dpg19Z+6kIMH/Z5LY3Wk9vj9PDhg3Tv3ktsknXr5AafCwCV15XmqOoI73GMA7wG/ep/702P112TM6N72zHEc9YMQJyvsvet02Hr37mEM95mQOeHAABxX+4C/PV3F+nDDz5gMwKCXRuw+7CfAggKEAzjixLg6Wyg87PbST//+c8JAAKYXazVPRx7E+sBACDP1u6CJcDvvAMmTS+tzj6P/STNWwPmuCezVHguuylQMILMUJtakzOYvdmPsIRFnC1uHmC7n5sMTQTSeB83Jf3a58NRYoXChx9+wBJ0JE/Q5ATdu6FN2euN0pMnT2hD79y5m3pDlP42vgoCW+4dJ9oi3mrE9wOAiuYnbr6TEy1rAWsoxaaVjBJBS0IsptoH8B/x52qtZI5ADXyvm2DEuYvEDv03NfdazbD/1gkAIMe8I6kXS7msJn4WAa46F3tp3Q8G4uVZJLQFVIKRTeAq9OqYKGPCTD6D17mZVcPVJOyf7J6Z/ug7jNeiSNDxfrOGrgAgdmTG/obOaqHnyDWDJkzBdBUjG+e5S+XjHEoh5WJ/M84nJiG73XR8cpivi/G170O94/4gnXTRxT1Kv2PcMf+XSERAN9ylswM1nbNWrs9vAIC2d2QVLjtKoAQwAgCf8+JS9Y6atF2ePxMI1YMe3Tydnj3LAOBL915KN0+gm9pPo84mnZ+fp68//4z+LHVnCdDCh0UzEgO+jQFiPIL9Hsx/ny0+9zi2Ln1lZZISV+X8Zo3OPb0KdgKARcLL4+bE0C4AkOtlMJJG6XqTDo8O08//5scBAKrCoR/UVAOA+Rls/x1XVAY4F6RuUM0DPwcMwH567Y3XFc/F3EZl+w8KAHIg9xB3DLzyLdR83QZad2IdPwAAaN+T8xznUGYQ2s5WACCTAhWAXPoSuH/u35x5aTMXm2dpGN3lSZmvvQMALN8HyRK9BOzm9fk//q//R+ujuTTDDlruLrYb0mwYVG0Xoa65brqqxIQZ+SxEwcsStpqaawZg3UVwD+MxP3vt+DQAYJTQhTZGOVi7/n4941CfymKNhfYXF6upwabI7wHU/N15ve4zIPkmf1gA0KUtpdHbNzYCZJoSMG3K57sfXzOLbW4t4H0uZwW47sk0/LUZgHzWysnKc1fN2U5NyHhzDQDuEi0tDdqWBqG7o0VnUexfODgkITAruGYZ27PzSfrggw/Sh+9/kR4/fpJWG1Dy0c1OIroQJccLJbZk9qAb6mKeJtFcA6W/7aCgBvpF6Tf4KWdIWVowbxZTOTrHx+N07/79NBqhaQIEhHUArwJUGVBzBECDmIBZYyJTQdsOARwEBEbutChRcAEYvG5oNZXr2UaZjseyaTTBpibOaEXQBM0ROpbZqVXgbxaYM9me+w2cB2Sol3Ba4XCIAVYeHrwXMsPgZNWaDlHqQT3DYP9ldqO+xYBOHaSpVFrgoLR30C216vpdJTp87pdjYhYgA1o4W5MJdY3gcIHBxhKb6JyKkldoGw0GQwGrcI4BlAGIgYaLM/qVEXFAh4CFDIX6eNnTpOAqO70LOALQ2yobzGYlHGCXBFVMOtvxunQZGpRs5sBu0xA/V+BgkeKcGQ47iK5+cDRHAwWUB+ye2LDI3IQuMxTMrI11wYAMjhzXphgeBGCRcS26ujVd3jWQGWT2OBYdgBXsxbqI0ikD43MyHcUsyzYarL9iH3kv8X7ifbl7cARcThQYyPC5XQf6z3NeSNtUIBFZu+F0Yzz/7lc/ToeH49RfLylWjYw3SyUh7g8m6wpsgkW6ffdFBugQjQcge/r0CQO3+cUkM4zZzRdrnPtNwCdK52DHUIkmOwxWBAI62cjBGB2EsY+DiRYB+prdFRGsLJry4UJKwJn4HNAVwAn3dTBGoBGJQAZMLTAkLi/E/ANOSPsQ2krDsZrzoPmAEmZieIAxUzaY8ekJ7ToCzdaCCSagA2yXcJf7TeumDQA21w4pgfi9xfS9HwiQs0Qo7qBonkSA0CBEiNh7jZX+nhzlYJ7vYGUwYePmQbEH6vPS44rrKymw7dcK3PRzNva4sb2ND4Lxha1T7qfDc8zdjbFeILmB/T+dL9JHH32c/q//579QkwqAE5tCBAMQgTG7+QZQB21Xzi+ZZ0q0cb5GAHwH/DcYL91xSi+99FJ6+eWX0kv37qW7t0+koRVNjFA6hxcgDet8qfEQgl/MWft8qO1rDuD3OoGV378HAMwJJlfwuISqajKCkS0TZDkR49LPyk9sNKgbgIUMwPkyffIxGIDWAHSCtGAAhv2kFlRoDT85naU//uEPGQAEA5Bavl5bBgBnk/TJp5+m707n0krrpfTTn/4kvfTgHkt+odELsAGmBIDG9PJCTPDhhs1C3n7rDe7jg41KcKktx6Z6WpOWFsWZqr3QzJPHku9HsqNIFjLByDVdNxUyg7UJBHVdr/OwsWTANwlHdF+FX/HhB3+ifblxeCN9++23LI++d+8+z200W7mYTtIbr79O24vnpuxCyK74/ni/jou8v1zCHOvLCYQsUREMc5Q+6nphZ63hGM2UNklMxVILUudySCTE2ZG78ca+QBdb7o8wA04IS2JlwxJXSj4gMRbsRY5PrNvDAyS3l2Scwl9eLeT/TRfyE5bLqfzZjpLGYm7i7NB89Nbyi73PfM66GzO+BwysOr+FUwAAIABJREFURdyP7cKa3YnX6XAgALWJ25xg0jUzAyzHn/aLta5QCm67ltlxjAOUaICfwfuNtdmzFh0Y7stVGkeCXSXISiazg3GsP/yczEUDED1pBOKaYDayuzK7+wbbroemIDhT1TW71xvwemlpnVOMoyQwQFBYb7DXF+ny8oJ2F3buzt07CfOC5z9AkcRqnb775ivNx2ouRrClKaKzsBdA3lsB+Psc015R0yvZU70sIVMSIzierlirmnw07n4zTzatWgOWytB4AAAkGcOVHFEdAqYp388mIPJFsSd/9rN3KPuAfUa/MboUNwCgfu71i/NZsVrbwPt5AFRjfV+idLrbTa++/hoBQGvO4qzhWUqtxg4T27h+fs6gmO/TBPR+ryt86vjdfkRzjuh+y67KXsf6TbsUODNyPX9x/vTjwWu/dEuUNNun2A/x7xoA5D1gn4c9uQ4AdEKmGf1GT5uVSHuJZ2JM+vf7KhlLu+JxaQGA/+E//u/5SC0dNA+4tSnay6P5118KANYMiE7ustlekX8JAFje+y4A0JvbgeIuwOaHAAC5YGuR4z0I5vMCgLtKgHc5tSWItGs8/LN9Wjkao7L7sT6RS4iKMuPnCer8ff9fBgC92TmvO+axZuzs2kP7DGPZ3fkqABDUe78YuCSV2AJgYwZ7NWcJ0nt/+ih98eiLNLlQRmK56jOQnszxp5prsFECKPrz0HfodNI5tbI6FCP3y4Ewn794AAOABv9K4weNmls3b6aXXnoh3b5zh6UFOLjBSgRACaCGmoDdJEZPTwcPIl8HcRTbThD3XxCUspYSAbasdaWM3GAgxlUWR46bz1oeOEiR2V/3eIDi50ME0HFumPk3R4RWAds+tOioG5ALgMUBNEoLCVTAhSoysyz/ZImbuv+ZgaXvaKIbgmLB8oPjaUFxfGdZMgqnJwe9EHWP+4B/6k6m5bqDY1IGEMwFVZ0AATbpZwA74Ghpbp4+PaVmEJ17PkOPDEB0VwUjAC84LAQCwxHYBQDyfi12/QMCgLv2l8ej2STheJkJZKZdXtxiypQAIMfLmT2Uc8L5nKvEpT+MACq66lmLycxCAIAqAVZJGZqCZN0jGg6U6Ihp6rWp+dQ+tRSEGVdliRftvAPpLDod5WN5LVWaWw4wgzmFPoO8DYJd6HYIAAOBq9af9zAYLm7Q0Bpna1RFIqIBBOM+cvOPdoY82/+tJlztJgB43z4AENpnv/rVO9Rm6q3UdbW/WrD0Bw4wgfhNJ52cnKS3f/JTvm/KEu4lA/TT09M0fXZOZxbrBPZmGTqPKLEBsAP/hE0eHKQFgIEibu7PvkoMwUjmOFr7kM0DGhDfNqTUfOX+QrdKMge1TkoJA/x+1FeAOTmHHucpS3/xOhhAj2pAAIBsDAjio0SYZZ/SpqOTTiYIytPVbTF7VdF5OHc5jCYbtl8OyMq53gcAyv563qSRmYEIaxBFAOiu2E0pftEApAiEBfRBw1F3kJmFGSBvMzb0ngLQKyQfWs8Q+8rvL39XAoJ+ntLnsd0s/RuMpwDjCAwDJJAkAsT+FegeHB2TIf3ub/6QfvOb36TTs3NKKGzWfWlEQjIBdsWM3kiQb/oqKQRThpq4Q/yJwDmaeBxI++mFF25SE/ClF2+TpQVtXthsSw/MV7NGt4j6GgZGmhLq1jmRGeHxvl3GVQ5A6zfw5yzBsfN6FQDo95QM1asAwLIbo+yjvn9TMKz+XACQ6wbAQ7ebnj5bsCnact2n/+QSYABSmgdpMUI6AUDY199JGw/L7bXXX0t3X7xDTUYzQg8ODmUHlgtWPvQGq3R8dJxefoAk6CgNlo/j/oOZZeZaNENEEq0E9OwXeNzYpCE0RsnOLrQD89gGKM5xivXF7tsExaJk1vMYCRyfM2j2BX/r4w8/IPB8++bd9N2333KskMhFXASNy8vpJL399juhfycAEP6HX9m3MaEk/EZI0vi+yv2VAZVoUtRopUWzKwfy81mUzIpthhJRghIhvg+NwFY8hC6+7OIZlRtzd1vVnUJ7U/eh36NkmbYiHkRJk4axCj+AtoklwE2TywYQigR3ZnaHZAu1ZNfpoNecv/g+7Gsnh2h70OUbgJctd5QaL1bQPlyl1USlzDOAj0xGCAg1Y64f/jMsv67f3I/OCRlYykYU/iclHQAggRAQFQAcj1xxoVgQACObkGzWPHcJeq/X7KqN+0LTHOwb/IlXf3SgcQa41++lKc5sJOzQ/R12yX5MdO1mZQnYZhHm4Nymne4qPuh0D7J/ypL49YLnPe0emzwsee4/ffKYTG2sawKPAGTZ/CuAdTTOLatuwm+pme0/NADItVUkQQzI+3yyn2EAEOsK66IXWpD4t0qfE+3Lz37+DiVB8FRkUs4mYhYHruJ813YJcNvA7wIAMY8PXnmZ40mSSGgyE5j/ngCgv3UrMWwAzQz1iuiTGcQFoaGNcfwwAGApx1KOUM0A5JouWIDPjRdVCTgnHnS9bQCwbGhZ3k+DF+0mTpUlwLsBwFziEQyCHBj5USuI2O+PDMsuR4CGJTQVmhKXdhfgXmfUDFxQrGVxt5lkZPNVDJbnYQCGaddlXQoXItkugSFqvZOVF/dRdP3lc2WGX5uCWjMA8wEY3St3MSDs3JYTWpdStrfnrqXYfkcNAu4DAN31Kd+nx+UK5Ll9n20GYDk2+++5+U3TZbddQp4zv5mysJtZ2Kk0P8rx5t//ShqA+Xv2sACf59nxnror5K7PtQDAqhsskaRosoA/IVJMhsBS3eS+/PJR+v3v/5A+/+wbHnD97gkBv9ViROBrEiWGq6m0SOAQ4qBU6UovTUJEGKNPx8IOnAPhrOnkIC00qJgBX+UA5NbtY2a+b904VIZwLaZEN40lXt7dUCR4tVHmHMwoMtGihKQEQnAPKINDwD8+uCnHBhn0YMoBYANzTww0ZfwbLcsIaOgsCugS8NjlAZoPlhiHy8vzdikrgMIYG81fWBc6XwIcFDTCAWnWrEtaPL90VuGAp3Bgq/JVO/o4xDE+BPqibA/PVK4/lyrQMbAWB/VT0O3RmjgCtkoCBcdto0yvAvam7EH313SWRbYVGlbobIlsK9YOHIDjgxFLzA8PjzmeAJDEZFG3O9mD5lvhKJS2Buttd2Ad6/o6O1SdExiB8juzHYELHU1OuF8KJpPuJ9aFz70cEInFSq3Hgom5WqjphLX8snRGBKIp/jTgMeolMm6PRiPuTzjLZD3Nlb31eYf5E6DicdJc+zzIDIkAWEkN4+8LbTp3pgvGmsfbAIfm1kwKibLD0ZH2pZ5zGd2yzSDN2lFVebqvndmqIRptBocztD63tzK9ew7w8vxyQOR5pbh8r08x/l/94h2O53J6wVtBQILxVPJjlUYHh+nNN99Ix7dvSSvo9JQAydOnT9LTp0/T4vyS9mgDxsZ8llZTdb90ZlsMwA2bTVCDLYDT3H0W3dapByktKzBVFEhInN6sRfsLjb0PJkZ/JGaLNYkQoBZlI1h3uM+zJxe0U5ul7AueDwAStP+Y8Q/tziWAT5QkhgYggCOMD0AjMk8t9WFWZeGAMtETnmJmbsa/PR9N0w/5UmamkrTIbp6hQZYddj2nxca9js185+cioCODzkyvihnmplPau42T2wLtCuA8+3kVwa9M3tQ+kuxoWbLTMAE1p01iwGtx3hHDlJAn9h2SPlE65uQMz00E071emiyX6d1fv5t++4ffh3bTDQHVcwXRSEjxZWZmZyjgCSV0AACj1A8liFhvZ7MLBrwvvXBT51dnyVLglx++xBJNaC56fA3oN/YXWoIqAbzutZ/B0WYQfl8AsEyMPw8A2DBMnLELALazlgZgZgCGBiCb5wTYQQaSzlMkomgP+2IMPT1fpv/661+nzWbIPeb9DJzEzHgylrprnofzFaoYFmyWQS3SA1U2wO+EjYdWnrrCR1OgZ4857/cevJBef+21dNI9Vxdh6JVhbvNwtgGZEoj2ecCYJfw/7Gv5Z22GnxpONRUIZl95H9LR4suVWe3xTNB/XizS5198zPX70gv30uXFZZpO5+n45ITLFEAoxvuNN95g4gH3KvmD7WRO/lmc24u5NflCq5JNksoSUCMBzflWrlUAG2TIpnmuJKAdSwLBqI9H5mH4r2bSReJrlMZKmFgqI+JZB+K9jfziphJFE5TtYzD8YZ/gN0NNJwMjtL27m01kzxHdw92pG2dvzBWYl3gONF/BK8eNTgyQYY5OuS5BVUyam0+E/R1mP8bNqrQ+0KwILwN19gEahrTmD4x4/rlAR/sZpSe4ppISEZulusFSUgL+X+hBLuLckq+hknj8HUl2nMuwmQD+IKGA/WIfEBWr2E/QRlYS2Ym4mP9C4hEA4AbfTc3ABgg9OjxiWMR5X6/SaDRM58+exHrcqLlIMKSbBEJUFsVAZ0Clbx+4sS/l+stN5GLjNpUOTkzsrvDJ198yrBXxKRKKrRJgSgUEgJ+UQMIL9v8nP32HdgsMTkoUhfYo+ykTw9AAyj9Ql2itv/YJkJvwJcQfizRBRUW3mwFArYlF6vUQJwYAyGZ7uo67UOeE+J4S1QbwDGC8Ovdr3ML72Hdb4hd8775439d9bgagvsHfXzPlf2gAsCRylc+8D/Brz1Zbe7/8XcOo9k+rEuDMAPz/AQBYOnyNQ9cEmmWpWEZz3f0zADyLPXs4twL7nCFp7ygjsGYy5gVclQr7HuvSh3rCm3/vBsbw+/p5d12j3kC7dLZqR7m1wCpE889h/3GDZQDp+wGACLR33d/zlgCXGeXyubImUGGgd2UD6jLg2mBdNXacowrQ2/ksRZBcMz7hcInxIm2n8SGCgE768tEnLPl9//3307NnZxRTpmM8VXe2bjqhcXsWGlMohSOrqy9HFffBQ3UYnTHB1GKzjejgFQ7jMrQGtd4whwIAAZAwsFnM04svvpjeevt1NSFZTNL5xQUdI1L2l+gmhy5uYjQsVwtlxnvSN0FJMIGtolRLAtAq0VgtJSKPg4SBjt9XlJjRubGfm9vLy3nG+6FxhxdFicNRxDize/IaAb6yu3bsDABSsiD2u/VFMqBWlF7qvoIqb0chAsrUmeeANoMEbIIivTc43dIn0zqnvllRkuHSZI5/t8MuoQSWCs3BOJLzwV8Ck8gA00kjANEAT7aFLN8oAEcc+lhPAALB3sTqYGnQjVsEuGYIhuEQ9MGsbLobG9TIgUcAnmBS7AMA6cBVJRRb9inPswOFdgYslxpfAQDa0ZTjYgcmrhOZcWjDqdQP5Yqoe1GJD9Y3f+a4PYlhAO0z2rdwfEfdDR3eMXRpAAIOwNjqpx5AY+yDAAz76O6W2axqYuMAZpdNb0osVdJnEM/lSnUpg/eI12PTlKGxfgLMK7Z3wfTbdU7aWcKTq6Rb42cG1/4SYAfl7ZLL8llLwMf7ECXnAD7+7hdvi0lw9pQaVdC8QcB6cnzMcUPpL0okewcABULvqttJ3337DedzenqWtS7x+80cbB4BgGzOE+cbAg1qGAXTD7Or/SIGrhlz1toCVlyuVa9xN3PJmp6DUZS1xjhAnB72LWz+fDJNX3/9VTp7OuG+HvYO+L0Imo6Pj8n8c/dHzP904XaUsOViLGG90aay9MnUV4nalzpjGHMHkDUA6Pm4CgBEwAENLe6jAgDkmsOV8Sew7ugWqGs2ACDXUMvpLtG7hiloW+czZxdz1vdbZuJ9NpSAcsuXCQCxAcoFAJb2qUnwar4mcR6CKcPETNgz2GWUBCIJRTvJkt9B6o4O0pPHj9N7H7xPJuDFRSRQ7cZtUOqIcQy/NAmohwQH11+ws9NQv79cTHgWvnj3VoL2Vb+rxjXHR9Ike/jwRTLsEQSbecxzM5r9mFnicSj9jxJ02lupEKVy/vw+AND2odQApL2yb1NoO35fAJDAUwCAs9kiffzRRyzB5bq/AgAkwziYds8u1ukf/uEfUuqMGFAbAAQzszwLuwMwbxZp3R2n2WyeZnPo4o34/QT0Oj3aewSksPMAAGGTnRhaLOHb9NPdA50fiJzLcxgAUJmAE5AS7+mCmaVzAj4V93Y0AWpsZXMO6swKe2yNt0gUZv87tKjchKaRQtHnHj/5mkyju7fvShbkYpqOjo94vKCkHQzWBw8epEv6bNEwpfAbyn3G9exmJR1IrMCvnEeyNqheBrqgwQjWXZJWruK2IqEYJY4ba15bboXMOTXI4/t9L9El2E3yukuBTwYAzeQrAUB9Xv4mmZNk5pnRHsy6pZJnKdmeW3OzIdC07z3mJ859g/M+LwXEFU00Kc3TMAy7HWlLbtYab7PkzSCmajo0JxdRQl4wqGBfULlDBqgTdllX2mMbiZtgjeJ9JcAiyYJV6q5FFkDiifsvYgecHzi3qC/OUmN1JAcAeHEJAHnK/XUvmLB4H3zoi6cTricw0zGeZOKzwzQYur20nAWzfQ1AUq1YAGyxSiaYkGDgAtjiuYMOxP1+evb0u5DE0TkCRjRsszQi9az0qwsAUAktd6hvA4D2z/9cANDn0vMCgPbDnHCEPeL5iuZZbN6GknvNMebhpz/7Me0d4ic1qZuo23QAgJ1OxE2xHp4XAIQWPFm/Dx/Q5uClMv9o4oXE9V8AAGb/6QoAsPSDbU/sz2Sb8FcCAGHHy3PpKgBQ9q3xZxv/oe3jln6jGcf1OWxt+9p+1v/efz7LT93LAPwf/uf/jXeVgae4smSkGyZeZsZUjItBjRxXd9YAX7sBsU4SJTgz6qqSD4uh1pRKX627QwRSt7AfGCtvsRcZn3xwRsDZlHTaONggNgZd37KPqRIHRH0fXEiNe2zR33pCy8VmkKk8+Mr5qp14GQiXYF2zdHgItA/VehFqfoIZWjAoy3nzZ65aiByvbcmdfIMlu+W66/hDpjZnLbPq+nV3o/xsBZV91/M2GnJNiW1rP2Qudcyz6b8Vo+U6ADCvhMJwtUDEcFhYrhpMN5UgCIxzCe6toyMaqNXFND368sv0u1//Jn3x+edpPgFbpps20X1wMzgUqLSGuOsyTWehcRTOuJ2UvN+g4QHnsQ+9PJV2LMBIC3YXcoGg1C/mKk2DZAACn1E3pcl0ku7ffym9/fbb6eRoTI0ua3jBiWSJzUqgBUXzC8YSmBjUOuupwYeYUdJj0fioNIFaRjHmchjURRL3ye6+0PlCl0/8m6U76sqHgxXfv1pOxXgKdhd1B80EbpVFREY7FmYJNnrMFFSFQ1E05yjXQG3HSlHbkp3iZ+LzumGGy7JK3aqaAecSOYsvU+h4O/22D6gvWYA6yDBuYjdhXrEvMI7TyTSdPn2aJk8DOB4OWIoGp4rah2nDdSJ9l+bgy6V8cUuYWzVVUSmsX74/OGQl4KR70vzTzkWpCv9uNgTtVdPYRKBRewz8TSUo0LaUbUbgGvpwywUBQmpCBYC7uHiaAVKAs3hRA6yjJhq27yja0/12CZSORwMGiIe9pukOHRw4a1iDG5UQIphkiSHKWaCNE131ZOO71CaqXypx0isnKNjUB920g8kU7JFOL843M8NiDjLwVZWx5rmI8Sy1//B96KrNOXKCoDr/StuGv7tJSanNRjuSNd4EvpNBs0LDhZlKXdMyvfzwYfrRW69xLg46nXR29owaNGDGzTfLdHJ8ku6+co/gR3+sAAIBEYONczGIp8+gCyQwF4kAZLRxvX7PzVlk/wD87UqObYbSvFy5FDszMaNLeT4vIxBjCWqA9AjMqSG1YCAnB1cllFhvCJI+/eQR13W3MxLbDqIbAUbiz9EBSgCXaT6P7phg9NJeq6QRwAACgx5Lz8EE1Bq1ptxwgNLUJRkaXC9mHBTazPqFKiT8v0G03sC6ZTFnoSnpZkT12rTd9P5DAKJX7a9Fl8pcedFOEDbrIwK9aJLjnzOYw/lR5AM4jizha7ScNp12CSwTSVHCrFKzsF2RGML5lm0zzmOXvkcQac1ZrYl1Wi60h5088p8AqT/5+OP0f/+nXxMQHIyUkIMUL5sqoQSOjCAl9vwgaAYCUAPnHs+/7ipdTi5TdzhIr7/xerpz764+D275bJYml89Ycvrw/kvp5s2baXwoCQLsN5zb08uZgmd8D1nJkgTBOUX2lAFwnz25MkjzFr0Z+HeOfZxHYoDson20EzR7xd4rPzOXjFeXtLZ2bv7FrtjDtJgv08foArxY8XndnAoJjzIBBvtJPVuULK7W6fxykX7729+mxTK6rA4O+WzQSstrN0AmaorBThSltxjji4sLAop4jfqHBM6wjshKnl+q+ccKHVN7CcxwAUyWiohEYU5EaXc4oVefhd3V02hqYOqN9r+lCFziCBumpmlNxRLX98Aab9pHOJOYMBjonANwIEZRN00uJ2l0cshk5MUlzvZOeuEO2NRPyeSiFMih4jkAelxPPfxdjEu88uyTaQ795qPY/W5OYYaSAC9UiNH8OP7I8x/+2DqSGDhHQ7uz7W+Aib2kjSPohJJp+nWqYBgNb0ryYhnnYlJC2pUTHTP6q0oGr3cAX9qnOp/8hO7ym+2U/Q+XYBdaqASk6FsvU2/YI3OXezQlJnTAuASTCutNe1sv3CdWC/2bfOC3K9IsydkrAFADgJynkKDIfn9UFjQ+sMZV3dp1BrDJVCV5g/XLz6zEwMW9goCAxhSwV/2RAMsROmszifyMCSyAdW7GBV8FwL0qXqSl2O9pHPI4bgS8Yy4hAzS5nPF66B5tLTwAi6+//pD7atRZElB89Pln8tk60dynlDcpYjdXBOZEcCR28zEVQLPHu4lTDQhrbhptOgO99ldzMXns6ygxbzRnwm4GsEyZX6AxlhiRH4J9DL8c30Mt2JD5eOvtN5gY7VMbuJ/WE1VGoFmm7HPTLR0/78exW8fl+d+skJqk89mc8/jS/QdkOrNqAn5SEnPa/luuVPFGD63NzKCt7bf9zh1nhddTXvA7/pKbmFmizDyAulIuPls/J7qAc778ufp9Tkzlc09v8LybgGG8pkxglrcrf6QhjDTMvOo8zM3RIi4vqkGafa9rlfexPTQxsZGwbn7f9s87/1wAQBu0vA8ciBSU1fIh/9oAoL/Lmap9JTzfBwDks4Zja6aIvy8bugIJLwP48nDTyjWDonGgy0A6c3LjC2qnDAyBhu247bAJpW4C7vai3gZZrwPurgIAy2tfdx2/978VAMjNB8Nnptf3AAAZ8HnDR+libTTAALLGAwG+CNRAKMPnx8fSwDiA5s9kmj74/R/Tu7/+dXr67WOWaKzmCILAKDhgYLPsjmiwFxB7ZsnqiCCBRV0z8MLkVzehKQCNF4oG2NxCoucWHZ5M53Qaj8YjHsrIlOL7zk+/S7/45S/S7Ts3Eyj5q8WMZbvsvIWM62LJgHQeXb4aCnop1o1gTIAjcsAsrckvZP3AnIruwMHAsJg8y/8I8Mnxg1MhBoQ0s6BVByAGGXncD56/LJGUPWqA8UxVD3H+MtDkNqQOCqj0DQDoa+DP/P4ozfPv3B1zH1DMewhnZdvIb0slOEjZD2zpKs8LAPq5PUZNcxl1B7x4ouYgyPxjvOHw8btDo8VAWWPf2sAafg5gEQCgxkm/LwFA77W2zmYDANbX1oXk8Pj7Wx0VYVsrQFDj1hzGuTkCHTCVimH9qxRd2ogsg56eZaajmrqIQevvt32HncK9EMJBadBAJeonI2A1CLyhFakSbgGH6sSM76aDHA62mbB+ntJ9KBNFpRSFmASxnqNJjM+B7kCOu0vwwGzg7woAuxz3EgDkmnCiMEsC7AcAy7Mn/z13oY2ArtSBM6O4AADni2A8dNYsoXv54Yuc7nG3ly4uzjNT+OjWDTrCN168xUAUgS6es7cSMD0/u6Q9Ws/AIpaNYHOGgcTHWdIb554cXM1P/VoGwoT1ojUk++buhZ4Tazqie3ZpO+bWyOuqe+xiNmFA/fWjr8igODg4oZ0ajY6COXpAgIO6f2z2MM1dt615iHXVDcYfzncwwPojlZ33o+TP89brjKWFGmWrZhwsAyjI9w9magEAZl3Gvu1mAwBy+wXjz+LxzR6NeTDAm5sC1CMbDm5OdjQAYGkrzZaF9hfvr9Id83mpMmuBILQncblOT0y55hU+DTXYAMroN/iDYxHzJbu8Tmsm5hqwyxqKBgDxyelkIpD64EBs0yh/m1xepg8/fsSS4G++O1Nw34EOLdDMoZq9BJMlJ9LjGczgXa/ntLlPzp+lmzdvpXd+9g61Rs/OnvK+ex11KQYgB+Dpzp0b6f79++nmrRNKN8ynCzXD4dkOeyO7hPUAG2dA3OOTmSthF/cBgG6KtH1mPR8AqLOgYCGb6b0DANR5EQDSGhIdAwLin3766RYA6BK4zNAA8MYGBgL+LyZLAoDzeXRlHYhxC2YV7UcPTZy0xwFiPJsKSAJvANrFYMFhXs0Unl9C+w97TPb3aDygVuDJyZifn108y+cKnznzLuR7zKcznWGhw8Znzfq5mzTqXvK+sJ/hJ1piwSXN5Rhq38T5FOM7X4qhZcapOhVTaVnn1Qbn3JznEQGoo0OVoncFRE0vz1U5QVu0YNdaagj3khhWcQ5jSVO2INjIQzZ7QJ1mADy2iz3pC+N+yIS1xFQl0eGEgQkgYDIqARcyFoW2LCo4WPYJvblOJ00ml2m5khbsejVigg3rHoB4v38ghr8Tqxt1WNUa2yZKlACgz02+d0/zh7x/3H0Vpeih9YfPDccjSltwTMOPopbefMbzAD6+zyH4v0NWhDQAYCdpPLmWYDsiQWgAEPOg9SKd5qarcDDa4/xlUt4yI9kCNnuStq9o3qamZNIyZmJ/PicAiKQwmbShSY2ElLsJE2DuqxLI5yZKySW1ov0HYNZ7FTYcpa94bpzXBKbOJ/TDEKdYlsYAID4PABBA4eeffKzqiEjgAqDHv72XPb/7AED7wd2oWPu+AGBjDwNMDXyjjC90HkVJMhNy8Cc1r06sYryUmOxx32NPQqLgtddeSbdu3+I6twYgPudu938uAIhzh/79XP7OYwRGAAAgAElEQVT53RfvsYs5qyMCANRaa2tW54SNJaPcNXkPAOjxr8+Luvt7+XvuD2sEOmGEkBz7dYeEnOxc+xuuAwBzJeCOhIxtcXnFfdp/jKELAJBjRmbebgAw7+GcCG8AQY9Vfb32k5mxWsY0+PsWAKguwHmBx1UyAzD/2xf01+jfQSCo5y1TDpsBD6bBliZLHADFwmgFpxb5rT7XbMB6ANv3t3Vj1Q+cIcHdMdMRpXqmlubyt0xnb6jR2pB6tUC34ju28tpVKSIctK1FXzB8YMR3MXjyZ3awe3RfDqj2jY+uUGsA+rrtYK3JbF03ntcBdz80AAgKevkc9Qa8jgG43e2nvU4NAOfnruYLfbKuA1B3jZnH14G91lCzyLOYMxlFyCBHl8F43oMos9h0hwwQn3z9NR3XD9/7I7tFnozGaobRV0Z2gRIKlCXhT7D0CACuU7cvDU4L/RrIyqzQHoAIZeUokh+lwDgAcFAjo8fxj1JZAH041P/+v/83ZADeOjkWE2ECsWyIA2s9uvsvckgKpjxKYaCCwYUM7ATAIoJ8iGi71jIYgCxFINNDrD866WSdraSJhA580ykdBYwTMmcIECTcDYFhleDUmfUySNffG3Hzcp5sH2Tg9wOABuLrtWLGzz4AUPPSPjVbTv2ODWcQcNe6qwG28j1m/+Fn1sJSSbjKv1UyrXsBs4AMy0VfGdavv5Im4NERgQY8r9cV10cldSAtD5W5IHBgeRQ3Qdsewhds7YvMJowy6QjUAehlUCsalPB7w8GqAUCXerefvwEBc3OEYNo1jrscDHT7xLrurOZilEUgxeABL+sfJmn0dKNUyd3ZBlHidTzsRImeAv8MVLCUtsnw4/tLMAUBIYXWg+uXn73O4WQtJjmS+GdZeteNEtas+VKcT/q+xr55XfDnASi6VNRz7IAil5I6Ad6SMWhu0iU3ZEwW7NX6PAUDBc84m1xyfw9G3fTWW2+mh3dvaz0maXJJO6mb3vjR6+no+DhLW5nJ1YeoOObuHGVIk7RZAyzQHJ6dnbObtZjFsBEIENT1F+MgYEbrGn+qO2gAxAEAuvsmpt92ScmVen1LUmCVFDBDO+6bb75Jn378EQFAZO8B8i2mSsT0eiMGqIODMUtwADaw5NASGgHYCrTC+AzVGb0HxinKhMUohWQEtUoJ9nTT8OAW7aTt+Bzzir1kxkHxfGSnhi6obUw3AEBriDb7ty2e7z3czHEAptCTCsZd21617QACs122y+eB7yf/SQ2odlJUe0vMWd8PEkg0O7kE1XfR/v7M1M+aqgEIuOszMvXUdHUpWQTIGwSr6PQpjVmAD2D0HAzF6LycbdIf/vjH9Nt//JOkKNDWF+aji3N63dhRMkMRMMS49ZQ06A+0LxZpne7efSE9fP3VkInQWQjJDZTcNUwmMGTH1HICM//wQPZ3uVB3eyCerjZgiV9ohZalu+U8ldp9GkclrPZrGz8/ANhaD88BAPpsxTmP5wEAuFhsyBg2AzBrkZuRZsAtupNeTtbpt7/7bVosBipV70UJNlm7su1MLK5X6dEXj9I3T9WVG8xaBMY//smPaV9cOnh0cET7An8O+/DgoJPu3rmTXnn1vpKgYCRGcwXuXYLJjdYky5eDIWgbn0sWUbWxmtDOTOczMg/RJIgMqli+AFQEGGo06adQYkDrHlptBqDkhwXgQvmOdTo66Kanp0/JwCbjbDCS5EwPGtLz1OkjMTsk4w82FEMEO7XcBKMtusdnfzfuA4ksfO/QdjMOWGvTAdigTxb7Ac0G6dsVGsK4/+GR9ku3J203dvnF+Rz2Fv4IgBHYCQKYQ/nTaaMmdJuV/FfMrWQSDnMJM36OLr7yhSMuKMBX/j4zAL1a24wv7Qn9rPQps//jeWGX717qhd2H30XJh0Ox6ZazOX1onHG4F3x+fHiYppfPeG0z7/OeibgZ8S3GAwwwjAHsBe4D8wMg7Qz616GHjd9jHWMcValjeYttAkgZV7USMhtp0QEA/Pijj7nuAAKC4YjE1eDwgPdDdlp08y196Nl0EaW/AWSC8QfplVUkjvr6HGwqxgKMQQKC6MSMSh+wOkej9NrrD1VN0VlwP37+0UdMNC/I9Nyk0cGxvicDGDFHGejxnGVEPjPodvnq2xInkcjOnNfm/OJacBMm+oeKL7RIYp1lBpvYltKGBHobjNhIyOHn1FqHrnSvl+4/eIF2aMTxXaV+AMB9d1GJBbK/+abe4DgdquHsAjyHzukyS6rAHwJw633s9W1Cg//tRNB1DEDbpnr9Zr+2qBBoj/82vlECgDneyhdu/yXjP/sYgNXnyjiA40TWb5vgUX+Vn2EnYLfF0Kv8nhILKhJiOSa8QmahnMd8T/4+S2H9h//4f8YKbwNbKKUpXz7QG8Do+QBAZKbKBb6NkEZNe6ubS/PduQTB0jUuzzPjfUfpZGtD7Zl4TRoCMwM+ZpQ0JTKtRflXBAB3lRbtAwS2Hid39WrAI7aQZ2lKtPy+Ygw8nyUAYvDHn9f9XQ0k+iv+KQFAZyhbC73WENiDOO7rXp21mWI4rwMAyxKWGqgpDYPHZwvMyV0828yQUuuPAXcuPd3Q4RqGVt+Tpxfpu+++S3/6/e/So0eP0vzygvvtZITuc+s0HBzSgZmvemr6sUIHYIj8K3O4icwSx5A0eum24cCGwWIZE7M9sU8CpMD74QRDg4oOLNhRvW568e6d9Ld/+8t0cXbKIHU+vZQDzHJeZVZLUKvpAucMh0cqEg6rWQYAEfg2+mi22E1Gm45dgFMAsMCMQBYaGcBnz87F+EtdOoQqS8F3KLDat9+8L1waVnbp0pgV7I8AAPUE4QBk+9JswgyukqEh8+vvF1ursX8eqzILWt6rgQ3bqtbv6jKqCmTe/Rk3Amm0AbPzHo6Ky9gwjof9EwLOn372uZqyHMohRwDEpgM72Ia0uwUAiHVE4Jllc40moD4bJX7VsyAD6lJ4Bi4FUIoxUCk30lhq/tE6y5AZz1qP9e/kaJnR5blxl158J74PmoAMeNYKfGZziXUbpMilnAHYm6lsABDzjv2CDLU12qw9B3DVWmt4H8YRjlYrKQMwB6UfObALR4TYVGOr3c2aY9RanzEiuSTEmcFsyeMvuzOU7uKcNYsiuKwBQGiXte67AAK5pgsGYLPXGlvYjKdAGgCAmN+jo2F666230ku3TmK3rQngQVv07gsvpJdffchxs0Ya7DjZO5AaWK/ScgJdTUgD9AUIFgAgS6g7Azm4FQBohheBNZQWloApbQEYm+2A2w4pwamuAnAAxmQwd5fpyZPH6fTxd9LSCikFPBS1jzoKmNwcoouOsgfjdHR0qIBqpOB2gMA85Asw/52B9scwEjRIdCAom8xnYsL0eunw6CgdHNwmUHpwKDH8mSe0OxAYEAAg7BnlFcwCjCYi1ECM5hlaMA5kAgB0/BQ2tgQA9fYoDdxyHHQddmhm4N80m/Baxu+bhFAp2N2UogKMaO390Doly4mAXQO6MwFSlGLp9toaWg5oMhhTScbkwCkYMgBGaA7jfZhjJKSg1SqNKSSpeukf/vNv0m/efTetNkhSdcjMcSkubSMBAraOV0K4hxIw6ECiJHOeoNF079799PZPf8wxA1MWYz0adukfoCkYm4NFc6vReEwm4JtvPAwduTEDfmi4EmCIRBv8b9wPAMASwODYBBBuG1nOf44X6tKuSkvpuiZtee6uAABxX3gun5EstV2s0qeffJrmc5UkYp6lXxx+RQTYTsCiCzCeGwAgugCjMoLj7WYsAQRi3gGgIhgGwPH4fEFgodftUOv4l3/7S9k7dtgF49visCgHX6S0mbEU++d/82MCUONgJmd+q0u7ohmEzkAlXOoX/fMknUn48TyXAvnLGpKZydt8ujwH3HQCY+cqEK5XND/arNPjbz5PTx4/SZOLMwJQg5EYw2AkYe9NF5IAgX0hQ6sf3Vn7AgLBEDQAxnXihJTPrbmYz+5FJGYaxk+A33KlBHHNzPLTzDMwEF2AowmRmYdungZ7h/3y8MGddHR0nEYDSeDMZ9D0k20DQxYlx40v1gH8ke0Q/eUCAOR4BwN+lX22NgBo37KOryzFgcoWnkeFn4j7wpVxH5O5ukyPBsN0+9at3KTrAmddSGHQThYN23T/4X/O5E8NoqkeAECcD+gGjPmeVRIiAB4tZSJtRmsy1n5AA2rSXoUWqhmAy9mCDQixv+ETomKIGqgHkDYIsDYaSXB+w78BoMfuvknAt7UAjV+xY/B6zUQKm/BAmxnl3WjiBaB0oW7bb7z1quY0qYHeo08+JiEBACDu9fD4lpoQer1YyqsAAHVPmofMaMvv1wrMNtHMqhyG25/KOztvQJ6BBmhzwrsNADbUotBUD83O1IkuyqG9aiYjfHHM650XbqSXX345HY6GTOx8XwCwWdeSaJribJjN0o2bt9P9Bw8okWL/k0zT8M97wHuKGBz+LsfpCgbgrrjZ58JVAKDmZ7sqw/5WWbmyw3zyvn4oANCT20hStL9xX8lu0y3cfvs2IWzb8quBC+30vgeLD9Xwh4FqS0F0GgBQn8glifnCBgbj4MwOYby/Agq3vtClkUEFrx+ms0fDzwyDppZen/QAN9dpDFMdqO4auPJndPhyF9kwmIVDb6PE7/VCrge8aDagG2wbSjPxMvWypqDW7XcqNuHObEPhVDVAVvtp/1wG4D8lAFgyGneVVV23sEuDsVUCHMOQa/KvoRzuGz/bFZQQPQ8Yu9OI1ay+XToHBQCIW3eAKQ0zOHjad310G0NGkOUV3XR5dp6+/fa79P57X5E58u3XX/LgG/chOo4yOTvsYghMl2K+TJcoT1uyBBgvl/CQjRJNPticg0Fn4+Ag4GUwXhh73u9SAMZ8MWNm+9/+m39FQGg+ueSBj+54PuDxfpTmGKDBz+GvKqDTxHVc8lGIIFtI2PonzBhGIIH40UEigjUmyeIwQpCMv+PzaFwBLbB6f+L93NvOiLgBQzhk/X7h6Oxgqjh7mZlMoaGVD4StxIaZIQJk3LykbHJS7mS8p2wK4ACaJXsEsopkyY71lRnM3hdFiSUdrMioZ6c3xo9gRQRWOuBD68uaHcEUGHQP6MB++fW31HiEMw9gA44JmzJEIFkzbMqDyyxnAIFw5hBccS3QsWy0l+zYtmzVDgag793vKx0BqrMUAKBse2PU/VkckB5r3kxOtMQ5gVJ4do9FCYrumYBOAH5kbVGjL5q8VM0RAE3iufsbBUDM/rNbJBq9QMRZ4CmWHwH/0ATahEPOgBcaStlvrDKHBeND+6phSNbjwS8ykJGB6zYzqj5L8f0cu9i/Pt8aseq2fcn7Ica6BLTrk0tzr8BZQGg34bkBSiymCjiPTw7YdfL2WADPpoPg/TINDwbp5z//ObXx2CVwJXBvOY9ujgbYY7/P5pBVWCUwD8CgGfaGLU0iMFG4B1nV3nSpRbBI+xYBVGbYUAJAwCYlG0IDB/YcTtsSmk6Ty3R+/ixNJtN09vRb2YFoNrGYicWQS7CzOL/E5wnGYG0NtWYODo9CtwtMcDSWQamvSuHwWkTmXnpxCHrn3FNIzgCwSKM7YgBGd8Y539clA80AoOeDdrtIAIkxLMZhk4hoMx3d/VfNBZpGHk1X6QDYipK91nqozsfsF8aAu4NkGZj787hfABLlHsd9gu3DRg3sviqgFPa0DPQEkmxygsbXqAOZWoQc244le1EWbOYnnp0s+ulMzbGc8EgbMmQ+fP+L9O67v0mPvnkiVkEPOnYLlnixqzNZzSqNpCRAty8tuY6aCh3fusVAe3ykZiMAmfB9h0fjdH52lhaTcwaEYPjALs8WUwbCt1+4md586630+isvy68ww8jdYVcNO542vJYGKJp3aNzb8cKWX/RXAAA1N9hfHH2tZQCAn36a5rMaANRd5tJwdslVF2C8zi5WBABRmsp91JHmlrt/4zswznguAIxfPVFyE/bw1q1b6Ve/+qXmhTIAaC0RYvydNedjtbzg3PzqV3+j823xWACUS718zkTAzyRXq/TUDKFIHAaDG2eGz3CcQ9BWpv0M4NL7rel6qt9DK7WeV1xnEIjLQU/fP704YyL19t0XCAQ+Pn1Gu3H7zoN0evokffvdN1zH9+69yPU9nV6GzEtohAXQBf8P7GM1SULiWP7QYr1QgmI6FaM+7Og8uur6/W7msU87G4Ad7VZo7kJPDwnsfnfN+335wV3avl5XgAabS9CuCnCCH1f6q0i8wxeWBA4SPO0S7MZfD0mACgACg7ZMbGGd6jpKHPc28JEhxaP5YPJ9jrLyAJ6iFBQAK154bjS4gpQA/QYD2rBfuWFF4wOvJzPaj74TOFGKbsAR54htG+YBfj6AW+mBKnbQvhIQVTYT4P0SpOshEOC5AX9E2sRLxidIWOG86Q8BrHbTEk3NoCc9hwTQkBI/BvoIyMLmkeEo/wfdh5WEicQs/MuVynrBmF6tpIEHABBxD9YW9v8bb4oJjfgQ4/vNF58zwYZ9gc+PD0/UtMV+WSU10WjLGWjR+Zn9bQN3WWt/Gz+gnbF/XUk8OTEAhiLn1Unboqkiu8KbiOKSaBA3kKDsYV91Uw8VC5AggN/e7aTbt0/S66+/TkkmgrfRhCWXAMfhyHUb51tpo/13h82WegJTGON7eHwjPXz4gIkA7J9e1uLWOeEEmreBz/8cZ+8pzc17rsBPmKALhl2OFwocifZzFwCI9Wom5Z4KyewjtJwNNSErX1niJn64Bbjt0Ro00JavtYepVwKAei+qTa4nW/2lAGB+/u8DAJYDBMS3fF0FAOJ9NZiyDwB07XcNANaadjnTsVUmdw006gnNC3L3Bvaz/VMCgBqn6xdBnBjVktU/t5oN7LgeN1BmaIamXRj1tlH4YRmAfwkAWDuU+wDAZlCuHsfnAQDzZinouOWgf1/wj/Ns8VyXZ4SoeM4AbZbM8B6M1JkSXUdPT0/TB396P/3pT++lb7+GKD4YQonMNnQZlcMhEXActDjoFmt001qm2UolEBb/nVLjL8rRoruuni0O3Og66PWEwAZBr/cDutFhPu/dfzH97Gc/S7PpGT9tcWJnULkmkTFdodxCc4KAJFPn7XgF48CaEtR0CRDIDSUcYPMa0eWsEbuOMpfNhswG6v2tlun0ySm1kZbQFAkwQX+GTtyexEsJAPKmqwMsH5iNam7bHsbANaw+sSoFeipgcWBd2sbW/isEi51ta7Qp2jder0V1Mi0ArtD68k0iBVICXQZQG0BQwDOeW1otej8ZA8hM9xUMISBA8IixRqADQAmOpLVsrgIA2a00wD6A1O5qhnsEOOOxEgASpUHxAChloPOwx9xvg4DbAKDXps8nfU8AcKH3BIZheX5h3+IQXs+g76WxoS7gUg1ncIjzuw1s2LGL0iRnRiHj7GCAgRcccHZx05jg3wIAEeCJOalXJAZi+kvHhGut0o1s/97l6gKe+Pw5g10zGHZnWJkaICMiu3ra07Ge6hJg33W5PsuS5mbTeD0HIx+aid0eg0PYvzVBjH46Oj5gpvtk0OHvFmtl+o9uHKV/+a/+ZVouBcwiMIHjOrucFMBylxlssAngH+OeoNkHAHvQVUDWNENoMyPrcxXJDgPTuE+LpTd+iUBtMETBQJhMLtL5+UWaTM4VgHY3tINy/KRNKUadxdzjz41Kn/wCPkpWWehOuiSfTIDYrwwUwokGuENJBCY8NhRgBwC4Gd1hKT8CPjITg42RmZy58kIApEqAC1AICeGiCztL8ALQ5DmQj18nUgwCmtEX+zcC3ZbxLDLctkdexwaQfZZkOxx+YLZptWQHSXQ6Sy1VwH0dHeFbjGomwQLQjBuzv4unYYKk2h4GAFE6JeAG879QR9hImGG9Yg2opFLrbb0cpC+//DL9l3d/n7768qs0GB2JFZ0DQ33RkgAE4AgBlpgnNLs5vTgjM/Tf/v2/5TrCumTzhp5KfNNiRuAZ94M1stygq2MvTRYXTNy9+fpr6dVXX02Hhwrs5vOptDPR5aT1qhLcxa81Rw0AuMsvMrCfL7mva2O9EPYwAAlaFNrIAFyZkAwNQODvbQagLgwAEPfXZdIQAKDm52KSCgAQYXPs/wCvsf9xfeyxb7/9Nv3+/a84nhhXaI7+i3/xt+x0ivfBHsGOE8jdiKGIkl0wuX780x8xIdlL5/xej+omNJGpNbtLBzzGIZe2RZOK8lwUoBSJVftTOaHSNInA+pttpAFZzguBBWrWrtPRSFIE333zVXry5PT/Ze/NnyS9riuxl3tmVfWGBrqBbrC7sVMaAiIjLEtyjBVh/5Ez4ZE0mpgl/EfYv9hSKEiNxpREEiQIggRAYu+9qrJyT8c55573vXyZWdUg4ZHGMRmBKHRV5pff95b77j333HPTnXuv0J7ef/iY6wYlugA6Hzz4iozia9cuK/ECYDG0cmkvopRczdsE6HDfkSzZSuswBwSZ2FwnNBjbqGTBuIVOs8+/kIgB4xHXl28JMBEAO85jgWgEfRYLNuPCnr/+3BEBWvRCKnXuwDizf2GbohuM+8F5xzNVCSOemcGQ9jrnPVSauKWUCD9TAIBaiGoQVDKHmFgMO+xEmzUk4f8jeQGbgv2JhCHtStgv+2b6rnU66g547q1Dw40M4Pk8Mw5RCszxjeofxNfSmO3Rf8tr0FrnrryL9YR1SM1ZqNPh7EGzMVQRLdR8Awl++IOjw0vcMwsysJGUn5NJi0Sdx5VafnGf3ZaArVobHPdq+4kyYDQLgb8IJqMZgFivd+/dlgRSS4na40cPyYR200EAWCyRLXqm+BzXuvSODL+v0rLL58QzAoBMCBbxo4H4XnvzfDEgafNXAoD0U5YCZSHd5O7lnP8gqjx3/XJ65ZVX2GSOrwAAcxOQuLDPrVKrtYlJGv4h1rO1hVlCPzpIt27dTv2hzolurANXBjie+ToAYHlOgOvv10UAIJ97T2WiJbTK2Kc+UjjP1S+/LgDohGB91tUAoCtx6nvYBQCW79mo5qmqgGw/y4qx7etv/mbrvrYAQDNxsoacAtXseFlzJq5bl8RtabBlYG4zgMqTnDOK4fDXM1AZngYAbA4QG7vyUev72jX5HMCOmQyRWQuHK3fLsgbhszIA88514LAZoNcT7kBw7/3tAQL3ApJxoX1ilDWwmA3BDnCrOdi+WQBw37NuLPw9AX290aABWDri2x/bDQA2zWYaE7ABusSvnSnmWjlnjHYalx0lmPX7zGjw763N4BIGaP3BYem31nTgP//0k/TRhx+mTz/+dfryyy/T4VBdMNn2nVR1ZFcBmol5JF2MZZou1UxgugJoNs8aMJMANspVqgBUAQYOcD63DzovrGBM4GBBIPnWW69F5k8lkVQlom7cpniyxfUNxNnx9OGDjJ6cHzHOmFyMMjDOc/7/6L653CztsXYKxhMlwNTRmi/SwwcPGAAz2EK5cnQU9gFmECaDEggQy9LgrM0Rmfd6bqGpGJlSzqXtVpEgwXc4o2YQEE0i5CBHYO39W4B++JU73EJs2/aODNEdDMANh7TSEKSTWwQWKEXj9QtmoMFUBvzBoMni2rFeNF/QCIKWyjI9eXrKUiisNwKAKKFcSoexHkcBWc2Kk7ag5lbOoKQLOF4IdsFoAFjMUnhlpmGfqY0SDtQ+KryvU7LeDLTwwYt5zaAn91FIU7jJg7uAxk/NwSq1IjPP7tjoRgf9JZbMRzm6z1FT9nPJUgB46HKNgMiZTor8d1M/mPZk/KKcGiLlEHqOfYd7xTgMI3Jy13fcFxgLrXDsa61Dj5M6VqO8WOvWdjOfTzVTpzBcBJYMgFYuVM0APK8EuLSFDSNwk40JhAUBxvTsTAFGS109Lx0O0wsvPJ8GK5X+zJK6/j5/63q6c+dums+UiFiOoYt1luZjMXVcQrmmLhS6gcc8r9sEZVzagsCNAXsALbm5T+w/MKims2l6+ug4gHzZSXyubETBe2OJ8Zls8VoAsQNLFilRIiHAdQMo1l6DZiuZ0ijvg+0SYwelXGKyydEvgSs5jQHExN7qdQdkvsBxx31iDFH627t8U6L9kZShBiDXgwA/NwexBpFLVzMgF124c8KmKmkqAeaN/RUJpjIxQjtU7RevkfoU3wf4l2sql48X2pLU96emojoCr8O++d9N4klXmk6DeZNLmTZtpUCDpkTMAKBtMEoaEayD0WugAuVrjx4/5PXBUAKg0h8e8ee7736Ufv7+++nJU7CpO2kR2mA4dmEnFux62jBEl10FymgmA+2nP/qTP9a6TwL6sE9xXQDn1FpdSk+XPgObYamc7uhomN5444107863GPwvlgKG8Dm/fL4RZLACR9WlMDNAdjAjNgJs+1IFo7Ccu63/vwAAZFfXSKi5CcjHH3+UlnM1/WK3zGCWc3zcHCHsJABA6QWvqaW8WBlIFzMQzTXc+EZrpUMdsvc+vC9gY6FmLO+8/S+yViMbHExn7EqOdcUmaespgdaXXrrB9XDQEQBYBqIl83TL3y2a3HAe2+pw7/MYOAUAr12l1TlmwPpPYjbDX+TZG5ULfg/8agJDszPajUcPvmLTknuvvs7n+M3nXzGJ8NyV51mK+elnvyEA+uKtFwm+ISFDzWcD3+6OHgtnHZUeiL/sX/o++Fzhl6zWw4g/m0oM3jdlQ5bpYCh/yAxq7BGOBTRYkcjp9NN4fJoWE3V9Hw2kBYjSadhPaPlyX7ai+/FglDvtWoOTZ2oAjtBOFLDveFEMxnq8ff9lzOC5dEUH7xOIPq215tAMRAOCSLhIW1F+EB6MjL52RxI784kaiPXgr3aYyNAFdd0rhyM2DllOlSDDdyBRaQbgYjkjYIfYAsxOdLsuX2g6IUkUb/iQdQkfEsxKnEeYBSYpF2KKAQCE393pRyJ1OCKgOM/no/bQ0aHsFyt5YN+i8RniArD5YKPKJO469MjBAJT0iuIDnI/yPZUgvH37Jc5LH4lr+PzTCRMrYLgzvmhDF0PYqNEAACAASURBVBdJINn3XAEUTbz8bzDY6Hfab7NkhZu4uCKmAqIyMJWbwWwC3gbA28EAbIgFhV/MUm03A4mmKKuojMA4AgheqyIMCRvY+xduXKX/0+uE9mnop/eqSp98f5GwyzFDxZhDokld4hVLDoaH6SUyAKVNidJyM/sxjI4jagDQa+o8JiDvwZVYVXxVMgDztZgA2/QMyuvXtrNc19kP3lE1Vb5v3zV8/vWDyGEm8N4zbEOqrukGfBEA+CxEsGcDAIPgVt3gdgnwDgCQE+uMxzMCgNsDV2SvSyAlHwhaMs6s0qjDMMffrUVTU5Cbboeb0E/ZvXIXcJPHoRUGIH4Bw61BV1CaNQj/iQDAZrE3C72c8IsYbPsWpBdWncGuxwqHrF7PxkisA3GvA1/3vIpco/UEoHYAgLs2o5uAYHWxlHDrgb85ANBr8lkMRPne8wxR3eUIAKBAGgFgw4G0FuZnx+mT33ySfvKjf0gff/wxD1o4nPPJZd7OqN+jQ7pcqdvtYjGVSD3YSctlmkWpLEqAWRYWhh6MHXaLCsDLLDueiTBuwfBzSZNL21IASXdv3Up3791lJg0OBBg3eC2XUwKXA9w/nAGUvwC8Q9MUrimVGpjB5+9DRk/OrPX8VDJgUFIZR7CjVOK3WiKbqC6y+BwAQGTWARqgCzKccBxcKEcAI5BB/UogArVGCCg2nRypCwIHv6tmIQ2YFto0Mflez3lu9wCAtlfZQScprGEBXgQAcudFwIHnzlo/octYA4DN/WonSOuwAlVir+Dv1prbZAE2pXrO6FmD0qVrBukGvQEdgclUjAE1h4FDhm57ZwQAHSRznENyocnzNbvJ1zTY6fvG/OE74IiASQAHzwDgOkScrSlYb/9dACDHNIbEmcISnNB9hkZXnEO52UaMp7XBeizdFAOwbJaD9QjHNCrIM1Dryjo7fHCgWeZQNEkhQBMAIDQAKVYNVZxeTxIAUQ4IB/CoHcxAloTHIR8BEh1XNg8Rw9f/3hmUREmINaHOs/cGAMnOrBywEgDE91hioFyD5f+XwCznpdLENAB4dnpKFlW/o26qhwd9ltytx2BSrVNn2E2voCnI3VtcAov5Cff52ePHWjsTMUCgnUatM4rfo4RGMgHIyIPBCoAZ682MlRyIuqQuAtqn46cMiCcnEwIrCDyYWCnipxLItkZZw4h2aVGInLfksDsjz0CT7F11H24y3fG5KNWHf1I25/HYIu5AQAjQA3a23x8yYEQJD+yaGYP9qy82DX1QooWxw/cmJZ4cKKKkHy8kTAzYl/MFJrVeuSY9AC8Bm7nLaE40hH9VJIi5Pt1kiEmARj+ylgwo/b4tW1zZaM5zJFgMSPozy8KfdUlzaUMsYYAAkHaMzKzNpInPa55hHgWX9EXpIO6fTWeCqXR88oTnuwPc6UJMmfm8n95///30w7//sWxpdMrEqCI4XmRGnoD7ZQfi7+hy3WEJ5jvf+64AjA6+b8LEAuYRCRc0lxmfHfPsY9UAtONa8b3LMwLor716L71y75V0cCghfwOAzT4Nf8qBbwUA1iXAHsvG5lR6uznBfkHFzgVNQNDdl2u/JSYSGIAGAGE3YM93AYC9NoCVBUt8wYQFoPDTn/4sLbDv6ONIdB/Nz6zjCLuCPYUxeTKBGP4inZ4e8/2vv/GqAJAEP2RABiDsOyRZhqNRmk2P6YccHh2k55+/ng57p7GvouKiA3YytOiCmV1IJpSlpPaXnDDWGQtwGGxmMNBV5u4XxgbriUlVaqNZmzD8tbW0oV1y58qablpwPL/84rP06OFDAoA4Sz6//4gMrsuH19h9/dPPPiEj8IWXbkQXZDxzsLiwTkNzugnwBayukwBpR4e2t+5Gvk7SFFYTEAH3/JwrV5aShPAV5CeiNDYSpasWmWiz8QkBNFTSwC9Zr9CYp4kz2t0hn+fSpSv8ifHCfszafFFSXAOAblrSAEbByDungou+YPjfy6hgYck4xx/JbzT70cxB+oJ2sbPieYOKAM4fkvPo+rxCE6kutbjZGX7QC0aeEr1r6INC+27cSPHQBsW+hSQAEreQvzAASOkfgHL4D8XD1t3nfStB75Llbva/VHoPTbWz8VluFgXNWSYUGGd0E6BXrLHh8CCaskTC3/NFrXDQ1AU45WZzoZkKSQ/clytFANTrftRYED4Y5lWl6KvUg+Y4f6b06SefqukNGcNKDKxbod2cmWexviJQhf9GlrETGr8DAFiut/MAwJL1hduQZIT8ACTWS3wCTFk1uFGC9MbN59KdO3dg7dSsCDFZSuC38meOwx3PBwBoe1Iz5nBuI4HlbsP90UG6efNm6g8Hkpr6HQHA+uze17RzFwBIu1F1264Bxn2x9zcJAOI+4DGdF+dv9gr4egBgDfDtAgXL92z416Wk3Q58pPVv/jKagGwdsA1jT8HrZmbAmdLmyzYRxr1AT3yPJ6CT27zHUbVVEmAKfvw9Pujr112IL4Kp6kq/dTBIGFDu0uPLlPM9QNKObnPaTFVpXtENSJbdGS0BkFuMvWcojcDY58D1Av+pvn7D/Dt/xM5Dl7N3UfzPs4pS1pul/lwN7NbA5NbzFDopNLRVs5hd90rHwpm84g3M8BUZbhpO+JYbFO44oPdkEC6QHszfhtIDvOYLZYIPRsqsDEcCr6aTdfroo4/Te+++T00bBLQYuwGbA3QIMODAcJDvAFRiytCu0QEyZce4ZYLmlRwbOYwO0Pt9BSMSr3aQSWK/mIOTOQ/M2XLKcQCw99KtW+m1uy/xfqQbuMzdQcsSLQJtYK2ghNmOpg/SlroS+wWwkGLZ1K8p+PnF/JQMtnbqUQ8Ez8TuoGBP9Xt0iBxA0kGI0EyAiMaC8woWCMCRKPGgRhQcqijBzAejMz3VfDeU6mofObChhpMbjqgEBPejLDI0+GaZiWXwlRoqdHLgnFfXZXONJtPbjhJZD4+v7X9vSWtVdgVaOPg+B7r+nMWrEbhobm3PIvMdLL1+b0R9RYBBLj8huLpcM2AYDFAipDIrfEcDFET31OIG6bCGY2y749JEa+yVLAY6AHUJXmYPCmi1f7APgCqZwARBY7hz048CVCvH1oxRrqEIJLjO0GkOJTazibpBouQHAVmUyKCknOsyyu1byKAH0EHGYzABrQmFwB1O8CAAvg47e6P0p526vW7qWQzaGlJZjFkD4+t03Kwm1h9LzADMFF1MNfe7zwNrrNV21AAjAjc+f8wHEgt4NRoqBYugcEQhNq959LqKphDuktfuCpAKGzIaDAh0jAYKmLF/YD++88532BV4sVaiYTE74f189Zma0yBhotI9lboisw39p+VSjjXsEgGYhZgxCkBXklboRfYdZd4xPKdnEwZk4+MzlRgDZMRaXhSNK6BXhPuO0kie1wEksaEGbE+IzMvHahggHmcwAPgq9q2bfXDdOcZHAqKQFvD69XVQanp4dJgOL18i6wyAIACNwZXnxPCA7hNZakp6EAgE85b9JmBvkgDKnJmX5lo/mIq5G3HFcM3NJCr/x6U+ef/a/7TmXwCABtC3AoB4H4Bxj125xwWItEzkzcvWR7j9V7GOo8lGdMn0m6kdFlqB9jNdAo3vhW1iSS2BZc0nxPVLu2R77z3u0kQE2ycnJ6m/Rol/L03OEKhP0roz5Lr60bsfpJ+997O07o24TmeUrlglBGAAds9m6pyOrqxYd7PVMn3ve99LL79yV+t4uVT328WMmnUAqGGPxSaCzECHmpFgwspuqTnMlcsH6Y033ky3X35enYv7WpfQrsR+6HYkQr9e6HlXsX51lm7LBeSBr5gd5XlV2xT+e49WVPPeTakAAEKyxeowOp0s06doijYVKICSP7yQyMRcQJsTP/trNK2YJzDr4W+dzVdiAC77UUqv50XClGufjLl26g5U2ruYar+cLSacv+dvq7uvGW7QyMXnzLR3ifxsjo7jvfR8XxpqsDEspXQpembmyz4CAMrz1u/T/2JCZKjun2AeU7szzoOG1duUZHN8cpwQ8QcqaGD/YurKRC/e3485Vdf0SWoj4oe/SuCpl46Cwfzp55/TLj//0k0x4ZdqHgBGGv2tcKg3Kz6aJnTQcMUr27uY6HXEZR0AUehEvIJ/ukxuqt7tjvOSIGBVNEXEuGOfYl3DR8Q5DEBNVTGRGCWzWmAJ9vOwN2Riqd8T8xDXgO8xDy1XM7dht2DDxaRvKqTwmKwiwTfDvhc+TZZvCf+T94t74ufhZ4Nxrv2mEnWA2fC/sJ5UWbGYSiev2+pyvS5m8sfbXZwFAq3x+R6kITrthGYcuM58gvXeTsNLhxyv2UJdgR13wP6Y4Qv/2346DD/t60rP6nnMJdyhQYlzjGM1G/N9B5DSABMVdgKNtJKaBc4CaFtQpzal7hrncBd81Bg3zOEyLa3ZC/CKXaelhWpNPVckwc5pjlSijYQ+GPeXjw7UHbe75txiT6JkPwiXCX4r/TIb9kJ7bzPOixgkNNGVImuaWuTFt6MUWHPhQDL2W2Ejee6mTQKSr+dz0Yk/W7tWEnDOfR6AIJIRZ9NTJiXgqrzz9tts/sT9NJ/ypxMKkPyhPbEGfM7bmUW8CSQYiMe+ZFx6dMjmUZ4PJcbaBHyxfnFdzLfP5dy1G6X02JuWMIjvzSW/FS60C0wr5yUz0HccOWXTPxPX8jwV55SJJbvKhO1T1LhDeR2ug2gKg3mWJIDIJDUxy/6L/O6GXGAiTpMCab7B5IX6O/XvWBHR1deagH5vPX619EYmW/3Zv/vfOeOZCVFpDDUX3AYAN2/sv20AcF/gkydyHyBXOSpGZ88DADk5FQC4pW34/2MAcNfmPg8A3LUJSwCw0UPZLM8sS1V2b6KvBwByw0cEkRlEvyMAmNwEpyWnBIwgjM/B0RUa3F+8/zE1aT74+Yc8sEY41OHcr+S0uPsu9zCdH+1DaXsgsFEGDKVdOEBn8xAjDxFmlOjh1esrYw1HmJ+LUltkbukwx8nZHQgof+76tfTWm2+m1kpAh8s383kSoNfpWA6au7LCQPpAp6FMul5mpFIEuRLsj8krDacBXlQo5TItBGEBbFiDR0ZazFAcTCxh21HKDYYOAbkuwE0JFpd72A4jgbuSJl9T/IvSJh7ACFCjrJWPEQehwaT2epoBQI4z2F4hvqwGA5uADLrNMeONQxYgQgGg+d7suHJNbGkybF6vBAA3uvVFAI3Y1iVGGoNGswTf024PWGLiscI9ILCB44BAFho9LJ1yyXhocNlRPU8CoWRQOSO9naHU4hB4puY1ziBr7jcdmi3bE4EdnegAAB04lPs9AzSFWLdtigFA3gd1AOeoMZMu4HzKkhswadwsQaLZTWCncRWL0EBr2QWYAWQS4A8AEA4zS/4x/0s3ZxGQ7RfLQwMQ5/7LALhLdqwh1gh9y9Hf9qjq/VKOoZuSZOZGBEBgAnJ9dKPUbKMZTjEnWB9kiG0CzHZQAdixpA0debvddOlwxNIzlLhwf81OJUHw+2+m689dT4sE6QN0C35CgOX0yWMxCVZrAh4o8XUgA2BrvYYQuaQOqEE0V1dGl8QBsKNjF/YQ9hL/DwCQiYdxdIFGoxEEKCFy7/dLG0fdZbmuzHAzsPxbAIBkuNSadVFK6bnx+tHewDx0qYF6eOmI4zcaHYrFNFBACIafmtFAN66TVgC+oL0Y652hV7CMyEi2Y+T10tbzNfvNCeTwC42sZ79HAWCz3OL9BgANlMb3bAQSheZn1oouzuGSKbWO/a+9jbUd32OgPwJmVxAwkA6NU88hGTZr7Tc354GYLZ4XQBptWdg3lGSWz4VzJZdXUwsO7PMeg3g0T+iCkb+YU5sWjLvZCut9nj741Sfppz/7WXp4gkYFHSbisA67A0kqTOZxToLp1e2k7nCQ/vAP/5ABPs4uMNbAEMI8jYZDrnmI5qPJAtYDpB0I4EUAAckA2B8EjhB5f/lbN9isYrkYEzBGl1sCA4GAt5ZYg520LgJnn7XZCJX/U4q7F+fvLl/Q5+TO6+RfXgwAfvHFF2k8lv3dBwD2gNkvF9wfODcMAM7nSsy2uwIOcZ7VACC1PU9l56ZIPCyW6cadW6rYmIXNiPEyqwcEYq6XhAqJbrq0DCaamW3R9Mv7FkCfxqNprsPzJkT/mcrAeFKyoQEAee9YnwNLHMhvQKAOoLHDpiHtdHTQFwATgCLikDI+WUNrDiAeAeclmw/hBf8S33uw6nB8n5wcp8ODw3R07YrOMAKSKpctAcDSn+H53hHQ5qYdpSQRrg/GFv2yYABimzOh6Mqx9ZONZeJzmkOGRHWvz3VL24xSxoX2DRlkAI2WKzIWnfQFdMFEUUsl+/QZ0dBsCBY1Jk/ngbXT8Z5cwkpGfAMA4prUUAsWn+2n5rYAIHlvIYFiYCTsHrrFcy6jay/OF9gOAi3RBATfj8QDEjiwFfSRXBYejf3gtpEJjGfYWM+SxoCfhudGwpb32VGyJ5pKMzFE/5SJMfgJGnZIWvB6K3WBnk5OKCmQptLenU/ifDeTNq47jyR3H13YYUeC2QcKPdYT/CjaTTL1NShaJ9Fcy4SPsLuYXxIH4qy+culIwOd0zIogfPbBg4cZQOw5kRHApfdX7eu4GQnHEwBXBQDme4uKDcQ3JGQY98suWeN3lwyuZwUAc6VFxItg5MkuSKtxfHbCfQz7/Z3vfCcN+4pzngUAZLywIW3T+GdqQrXieUKpikMxAJlcQol9SIAYAOQ+ZUm41knjfjeVAVw3/x8BgCX4p33juLgxEyXRYMN4FP/4bQFAo2T7AEA3TXGyzInmXQDgvnuLnacfOwDA3eDpZs1VBgD//N/9py3umC5QZeTz3Wwy27wB8oKva+EzFXrzc43WS5RT7GXamQEYTIk9ScatAa9Gb4v55+tcmGmMfVaPUg3QVd2AdwGAGtfNANxdiFlytgOY2LcIXHOeu3i6dDvGe+v7q3F7Voba+Ytw+6/PwgD8OgBg+Q3l+CBT5lddRubfZ92KHUwhv+ciBqDfVzMS/ZwXzVndRcjX8xjI0KBMRwccgi+UuC2X3fTBBx+k/+v//kH68ssv0tP7xzwQj/pyRIfRHRTixtKPi5KHaCKSO/oCCFyt0xyOCnTKFtJAmZIuD0aL1iMYgKODg4o5t0rDwSWWEPSGA5YIwCFBqdDrb72Wrly5mpbTsTRFrKkTaxgahLiH0xOVaMAxYbbUpTGg31eaD9AxUwmLyul2vUoQkAfJokWAwKCXAUA4svg+rwEHaAyMoxyMDkVoSMK5xfv7/XAAQyPQBwHfG+Dfxt5yAJX3bmT6ipIG3KfXgT/rMosSAOT7okQBDj0doSh/xt/w/8jQ0+6jDISZ/2aUSpZW4zQ1G5+BR7XxDQA2JXJmJjddgB3A4n7Kg0rjpwYNdmrARqEoekulNgg6wSx1thw/LYgvbazzGchZcDsC/DwfZgo680pNIDHo2OU3NI7KCoGdwabFnQHORKdrAjVVyYS/V1pAZVn15v0D0GTJSFx3tUSwsaAjxsAog8dam3h8BtYB3pkxlOc4AAqK1bMJgzKIcLj4HsjasQtrZCDjOig9c0CA96OkWKVDKjHKzRxi/XIvRWfSbPMMjuw4tzITM56nKeEKhm0Az16vzmRvOdiFX6G/CaDMJVVrdS8H0IMABQwlMJrgiGNcD/ut9M4fvJNufetlXulsekJH9Wz8ND15/JjKU2QmAyhlAkCMHQDpBEpCmwr3R9YtGYAofXIGXQAfQAKAqgAAcT9nmFPYUwOA0FliiWYd8ImZoXkLEIF3Gv4VulaGHSxtTT7basfFHQMB2OF6AZSpBL5JgAmMa6c5S+UAAPbYJGJ0eEDpCATrYOo6wYPnxb5ZExzopl7vgKwkACPY91PaWN2MAIQoOYwbtT6ae5c0Gszhv9UVEP53vf1DQgJAN16+Tj5vq2YhYHzsOydoM6MZgbsVQ8qgXIPsAh8MQNqw6KZpe0s2A/ZXaMANUTaL9TMTIHNwSQCR5xYAIEsxs9nVPidLLwBAdqNfrtLJ6WmanY6pUcmmM6tVenxyRnv6dLyixu9P3vuF2KVrNS5ZtXFuNusz9fqSWrh0mP70T/80rYNhbN1U2DECPF0wXOeSCQHzpdB8pN1bSxcPTB6Uit+980K6c/duancWLFmFnWYJbeBQYALpbN0z/uexAWPCeB5dpMF0QRdHjj3Wbdb3Q+lpN81nieN3ciL/ZNDfZADm5grBQBaDs5UBwOlMlQ/9wRHvtgQAsdbJjF8u0y/f/0x/R/f2Tjfd+/YbHBdqHKNUuI0y2GVmhEAqBefh4EDdm7sFg1IMNgAgTZMwaLyV5xG7HZOJFUzpVic06ALoi3+3IqEJYIb7P7SG1Vyq8Rdbp3M1ARpq3+NA4fpeBfAU3WjVrR4dpKMyoyN/CU1isEeQUDg6PErtnhIKcGhxn7A3G/Fk135dnKEBLDWSS/ITlaRZkYmnDab4j4zzas9v7OfCDnJe5mp2s8kUDrbhcpWeHj9motJ+SR+2jcmPdhoNR+nkVExylNpif/gcGBwOCKyjpJjPF/43EvqqIIp4Dlp2UU5LmxIAK3xxJv4s0cIETsMkXK11LlgKyNq/q7nGBucj1iv+Y3UE56etOcSujHJYaP8gUYX4gMzY3JQM7D81n8I5hjV36dJROjy4xM8jcYfXFEAczxkxCn0ub/nF67kS52vp8LVmCzbVWi/Cj1wP1bwEZwrOJQC5IDIEUxF+kxN9YPBBSgjnLTqY81zNEikCpOYraZ2bOYpzl4nopRi1V67qOQBI9gFWrRZsnmhpjV53JAb+SiXoqF2u/Q/uE5Q8wwcP/9sAoJlsrgyzZiPsvsD+5twr4wUwWcuX438zx50oyAxAA4n+nAkjgRegBBj2fXp2Sn8PCYa333knDfvqPm4AEFJB9ItzJVvcn32GHQAg5zgqwmAm4Vch0XTjxg02/GGCjM1HGn/UzU58/O0DAPMYeN9UFai1NJbPWH9uHwMwM4A3RnkbBCz/XPpN/v1F51Jz/+HH2Q/eU4lp5p8/58ocdzEuiRXVre/5Z8MAPI/919jG3XFW6yIA0N1lmo83Bz4MnTsm7QMA3a2sdhQAAPLzESjnhbK3BPi/DQBw3+S5VLBm5MAA0BF6Ro29cgNgkZYAYJlZ+OcIABIw2APg7mMA+nnrwNHEJgfOF22afU1RzIwp921ZApy/35phYTAvAjprQ7XPsBgAbLWk1QNnGy8w/77//e+nH7/7gc6npZhyvTX0qTrp8rDH92L9iGEiR0TAsMpH4UxO5lOxo4KRs1h2qfkxmQpkMwDYDm2twWjI0g6DcZ02tPLmqd3rklHT6bTSa6+9lu69dpei+e1wMHO79tyRTaWtdoTNqIPBQ/aIujwVA5CZZmRXwaJiJ7ndwcXGXEd3TAOAAFjIoCs0ijwudiDLuUCphbOmcEDAjFC3THRPLNhuJYuvuIFG7DkspJsnVF2/DNh7vjNYt5pkBiAu6xJQyuEjo1wEQPhs4wjJsYImXFl6VXbFVCBR18hWgBXWT5SvlTqAPHBDWweOI17u7leOHzQbPff4PeaM5QEIOebzdHwC4NqNS1B2osCHQRI0iS6YYwOAuvZmmYIo981kmLlqQIV6kbmJid5XH+ylhmIJANYAP9dQJMYyiFsAY7w3ZIjNWMqMLwTWAgAZ2KBsDP9eKRDC82Mc8CJLNkr2zWR0ZrYVIDcy5HT8ozkFgDztfwXTdgag1eaghkxAYBsoNSPGoc/TrrQlfs+xCe0f/V4sM5Wvq4SLwKcZWrG/DDxhpzJQisSMHZvcPGifJlKmGDjT75+xTlcSocb3wOm8fAki7QBQUK67SAf9lP7kT/6n9NwL11mSN54epwcPH6oUCSLi7IIrAJA2wqX5wdSYLVVKDMBazxkBVJzH8FPUFVWlVGjKgPmbxXzPTqcJLOfpeCxNUTu01jCOMjIDgM0atBh5aItWDD6v6qUBisLmMEgIeYTMKCwOMINR2L+zaOIAABD7ro/Oi4M+g9seGS1R6hh7FOcEmSVdBWwQ+8a6R6mVGbnUj+uGbEB8TqACmHBgBcM2OaA6HwB0AOfH837Ep+TPRMLDCc6cmK4Yg1XytNnnsZ5aZiJvAoDuvlwGXGXi0IxsMOCwPw/66r4oYK2T2r1oyhFJGTMOpQmEkll14fX9UAsSZ1Ts1+nJKTXKHFjff3LCpMmqhcYRrfTzX36UPvzww/T4ibr6pu4wM1S5lsDiXC3T5evPpe9+97sJ7U3J/IjGSbhPVhb0BGwjSHZpM+4J0iBm2hBICImPWy9dZTfJl25f5/NaAxGlh0q0uARSAMG+17llwTvYGXsvdM4fyDRbKAmFQIql2asOG389fPhEmr89leK6BDjHNbG/2hyfNQFAaADOF13u5xIApKZykv8FABD252c/+UgJnx6aMhym3/vu29FEAgm8FTJ/fMGOY9/0ei0yK2/eeiFdvnwpTcebDLb6jIJFZdOIhZodQeuLzKoFNHEBwtkxFTCNdcSzKID/2Rydn/F7N7OJpgkLlb5ebg/VZCgkY8D05fzSr1uxwgD/drO9Tgdlqb3URmn4ap0OBmhEMU/93oBJ4NOpSkvbXUmw2P5nyai25slM4U53GF25oe0L/bqRNH6jnBVacXopMbTrXOb4WgIjugpne8J9CQ1M+FZNoppXXIFV3k5PHj/h3wi0BsMezwAgHCAWADIAsdC2YxKcmmhiVM5QetnrpeHokOfTcHBEQAaAIEu2g1EnBrJoL/ZTYFtmAK2ihFj2TkCgAUAAviqpDN8HlTusWkCCD53doyQU8UMpWxMVOMvJPD169JAAHhI+p/OpEhqdHnX/MA9YVwDNrl67moaczxlPcQK8OUHrhIyYkX6ZLY3mQhhnHCn0L+Yr+n7TUzBIMRZH8qnhryLxFlrY9E26Pfol9B/mizQ5O0srNCFC+QnAPpSUG7hEQnAxT1NKrQAYVLNBbDUAuUhQYLyuX7/KJOV86wAAIABJREFUddqKLtnYL48ePuL72JUdACCBzSjBjYqC3MTI3W1X8geeBQDk3rVfFJmw+viG/7HR2TUyKgAAa+0/rtGI56DJKyct7ieuv2r3uN7OTiUp0O2u09vvvM1mN2wiNVMCoQQAdZ3w+czEK+Jb+9l42yLGHTgOmzt2O6y4QJk8E1tzlKA3AKBLen8XABDffxHBxue1tSjr46GGuxxvNcSI3UjdRX/fOoYsXRNN8WqCl+e0JqgZAHSlCdsAPUu8m2/AlQbbwF4Z41wIAP7bv9xkANZafy4Nrh20eiCadtVm7FULbMtBi6DH/a+s+VNpVLg7XqaMxsrY2+V2DwLr+933uYscj33NNra6be1hFG4DfGFQczepi+5gc6JpGOhIRta/YADuWkj7NAD3U093I8YNWm5KacXs3DP+2QH+hgFAZNDK19dlNmax5eIiXwcAvChTUBuUrfuL0g6Upon5104fffhR+qu//rv04x//OLWSNNQg4ooDcRiiyVcOEruuzRcqIUEgJ2YS9G3goAtEmyNjHIwXPOJ8Dd2rRZrMpCVCxxfgVkLHNLHfXApMh3muhhmn41P+vHPnW+nll2+T+m8hXg8dtU9yF0k3A0kSE49kASnyACJCoNrAZenEweFUx7L9AKDHXd050QU5QKbc1ViBGSpw+HzB7MhNL+I+0e2Q3cvYZVFOIA5S6/A1gNzu/ZmZUFEyUnZ/UyAeJRkuYYhyz9xVjgFhZLfZdbGRWvC13TUOd1BqW+K5AQB67BCQUscryrVQ6pK7mbvsJGvkqBQEDMAMGFSOHf6OwJEAadZ5FHBFZy03ddG6K8FKzDMCDGSfyWpA2Q4YbNAtglh1OKvQzNv1asbVDnEF/pkRWACzLDWyDlpsNHRLPe/l847gMIGLENmOEoayC3g+Q8p12RYwz0x+7CPOUwB0sC9yaJTwWkcgN19EN1gE3WbPYv7CEc7JNZ8n1PDEd4Q2EAIMOPIWSSPgrBL48j4PuLZRuqj3m3GJUMSAmIHfBuQMAJAloU3XaV13i7KVAxp/3gGK3t6U7+CfTcZTe9slEXUXvAykQfOJpSYtakwdHKpEFZpI+L7Lw076oz/6ozQ8hO1cpvlylgFA/B2lm2TSzlUCJqZJ8wyLeB5rTa1TX/Yz/JJ+R/aUjW6g9RfMGoia076M5wwKx6enYeOabq8ej7IktZmb0CCNAMD2LDPGq+YIjS3UnPhc8flVr3GCeNiD7iYe3aU7fZQMoaRtQDs36kNjrpt60AQEQyMSs5nBsharBIwQJ5pYckoWYCetIznQ+AUqtUXgSCAqOx7hD9YVEFmULwC9Yv3y+yqGmc6X4lwwIF1oE9o+6Z7CtobWk9nfTYVLw5rk56KUi0xAAgLSimQGHyBgaHFCSoJNefpg/AAMiNJOM3yiKVW7q4AVATyuB9AI+5hgBj40X7Jk7uTEXS0XPGvHM9nL+aqbfvaz99KHv/lc7PDugZqzLMGMnaV5r5vu3rmbXnjxphIrA2gK9nneUYwfKUIAWegIjBK96ZT3QG22OCs5T+EvACDDfrt01E83bt5Ir71+m4AVunUyMFxIHgTTUu6jcv3tYwbuAgPreGPLVj+DFA7njUx+AYACaHtsevLF51+RfdkPIAbgJ0AHlLDy3LTfDH05aJQt1hxvA4C9vkqqseTwc9kSgIVKAfhP7/74YyXquq10dHSYfu97f6CSa3ShRZfaAT4vgFKd4k8ILN17/U66dvVqSqunemQA1AVD2IFjKetQ2l/aepxZBTDCfZEdTK2x1QIJYGyaTeYdSj7hYz1+csL1gGYN1FhrMuvSUp2ikmORFjP5lPgMk0M9lP6uUj+plBZAHl7zuRizSgwuCZDz8drRVdia6+1IgHVGufsrzyOea7AvKjntdgVw4UV2TxVfAfDwfud+NXM/qjyuXb7C/YD1z/tgIxGBZ/RpVupau0CJ83KZBj0lSqC9h5+XDg9YMnxydkygHu+lFt9C/u8IuSic6ZHIanfkPw9GR5ScABAKJjW0OtnFPuyEAQAAWRhHbimw/KsN0LVdIxAMBqISRtAOp9/WXhNAQ2k2bBsAG557BFnbqbVYpi+/+DKtFmqehXiAFRidriRa5ogDZvwdmtJAO5sVHZxDdPGVn1hLr2hOUSKsio/lTBIYkDDCeIMB+OD+gzQ5RlfeHtcHm/Ow9LzRnMR1JGnS6Beiq/QasUlbc8BS7aiEAeADe4p7RHwzmUsDGMAYGH7QKsd59fzz18hyhwYg1uzZ+Jj3A4IAnrcX66rj9WQgJztQsW5jnPOvI15zBaMTqSUDsNynu+wZzzWfc9V6dkJsK7HtLtpxftqvX7cGPM/PJmIA7gIAcQ9QG8bL6864BfwN2p0LAEB1AZ5z/VxDs58XXoiqnmjWCOAdRAJ3944Hb5JDmyXAeU/7feUe5/3aP6x3hPElfWD7/NgBiIUPxDUb53hu7rol4bbpr14Y3+dKDunw168S1PX3yw5liDQzpP9JAMC/+Lf/sYJsNh/inwMAyImOQMKZo/8OAO4GALdWYPzimwAANzaDmU47HPT6HsrPfdMMwIsAwIs2cI6fi5v+JgHAeizq51+v1bbe2hu//viz9IMf/G362//8IwaWB6MQaSeot0z91ZglcNcOkf0bpulMXeSgEaSDWoCYnTUYbDpywd5ZBlA4m0Msd8bAjgBhiBzjMEGpGLqJwRGD04WDFiDQa6++ll58SYHGk6f3+X0U2C909UDdpiGzYV231RWP5c1ddpPigZSbFph5s2mG3KSjDCx3rW2c/e6SyvdCJ4mi9RqPWQGwbziIPvAgTwxHLfBsOaHNoYNDjwa7Kmmyrp+y2QgUo1GARXYZgOM6DdWeQF4AgA5AZ/OpxJJdRhaODsvI4BCSwSTwiA5/lKliP2M9ICD1C+uIzlerxTFHaRk68TlQKxlc7nLqLm4GAbfHeP95QNAMJUfUXWnYddLIaRhlWIsYR5ZChY6YxPXdq2/7Wy8CAMs5Uumq1jmDVDrI1h3czkhsSANEUwyujXBCVSorQBMAsRyNPWB0lNC6W59Ly7JDtZwTOAGwh3WJ9Umtr5mYuSsKkse9R4CjcRUAgZHkmBlYj1I3Mf+gRK794+d1AOQx6UcJIxmBAG6ihJgAIADccwJsZnaZsVc5idaoNf08JsEQZMlWqY9p5lczblzT2YHVui0rC7T3DMgY0FeA1e/2AgDs87kBAOJ1/dIwvf3226nVBYixpOYVGHmzyQnZGwNqCM7TaqLmIFivLn/E51FCQ6DOJTXrnoBcS2nEeMPOgQF9RqYRuhuD4bpIs2MBNxRmZ2AZjIyYUzMkbPPyPnPAsYwmYNaWDAkHd1t34NWcY02H7rIEWGu0Wev+HmhZ8XwIM4GfvM9+j3bv6OBIzMAhGoP0WeJMYLMQL2digUEc5qSRKmEgMwyNstjCDRNQgHrTvVilVE2XaX0ggx47urx+HQCwsSCNDdDvgkHYabSktJC397OYoNEcJ34ikEaAimWpJldi7CIgFGMfwClAUjEicZ6a4cRA3CWCK2lUsbwtXtQGmy85/mfjKZk644magaAUmM0K2gMG6l88OE5fffVVOkU3aySoqJ22SofPP5/u3L2TDo6O+L7hJTA3BdhyPQD4BtOJCQnJSrBJEYG+Zkzwe/oJZPctUret+3rx1tX0+uuvp9u3bggwmQp4h90wI3TbehfjvoMdWJ6lFwGAtXh5/V15fwQAaEmPfmeUTk9O029+8ymfvwuAOzR2BUwr2ad5FdMJ/wYAaAYgE68FAAiAfBkluwYAf/qTXwsQ7bWYkP39773DccOv+PlgWK9hg9brNJk+TUeHh+nVN++l5649lxazBzkALJ+N3Neq/FlnkBIYPj/R31gHVMOY5vIOxKKFbvScLwNuEXzG+zu9EdclSj25HwF2F+PiUneUngo8FpBn5s2gpVLP+w+eRFfXS3FWBOA8V4klNA8xD2gaIykYAZPzhYA4v7KUDRI/TBo3CRVWNARTkdekr6REis99J3itZ4z9y3PPEjkhuWN/xM3icN5jXC8fHfGcAQAIewjNWSZVQ6IHewfjgNJhrK/ZGGWlSKDLD7QfjoQ8fYoe5E9gP/tKpER3difkYFcteSF7Hn4T/RloHYbGo7UHczOvKPnupLDfkraA5AN9bSfZ50vajRmb+PVSCx3gkQhYrdWtGQw7NMnp9sgApOQDKmF6vej+2pzQlhkpKyBm0UAFXVlISGmj+VA/rSczfu9yvGbJe69/IMYkG021ec4QGMeZHNqUZNiuocl4Rs1JAIelFJDtM95DcsNink7R3IVlygN1OT+V5NDzLzyXrl17TgDgfJ7GJ8fp8y++oJYwS4+7Smh0M9XMjCqfCw0AiPXVAMtReh/nipv71CW7TZffTWCoHXHa1wEA8f1LJwS/YQAw4wJVpZH3JNYT5ykqyCbLBRMXaASCPbZEIpuSN6FxHBWdGd7K9r8BAEvf3XaktndmnG9X2oVmdSGxsXkmXAwAhuchu3EOAHgRdkA7G340xmfX6yIA0BVav20J8K7vrf3A8nnre2z9+V/8h7C+FZMrVqgvttUFuGqG0NRkb5Z8lLXanviNG4xvryfaIuBgytCJdmY2tCD8IDWwtZepFx+4sNvuHgbffgaga/orh7JG9neU+GIcygzCRQu5/Ps+BuBuZ2y7y3CpUbP7M/WClgNfvnggVN2hLyqN3QYAN0t8siOw46ZKw5Hjk6ZKc+dj7NPg85t/VwCw0fTSFb8Ow1RA40Jda7uj9OD+/fQ33/97Nv34/JMTAnzI9MERQQkG2FQH6TgdXbqUrh2hFLBDRoqcoXD4XcK2Qra5YQDOnAUBCIcs2GJJRwYAGrurQTQcxj40CPHdyOTO55qfu6/eSa+++mqeb2R0oY+Cbnvlq8mUxX6ghpd0qHiQuSQjO+MlQ64CmwzmhPbLLvuB+ZUotJmwyBr20wDl0a1WOp2GSHFRSsZSzgDVkAylRlKI3kuDxzo8Al0FgEisOAf0UWrDrsGlBmNm2AUgWxwMzPSGo8TDo92iA0NqfXwPGFEEFMEGpAPcBPYG/zYXejBnQsMOzhbuEWLvYCUB6Gv0/aQBaBBOwGUzf7tAQGS6MQZ4CSBrGDPWhEGgL8aD7kUAoLSw7NDDqQQwM4+AgPor63UaDRSY7WKTlKAb90oFcPCeoHPC4FrMOOvtWLOmZPh53EoAEECAm5yoBDi6+lYBmNi1Ddsg26kAmh2stQJQtF2AuDXvMxlMA6i4ZEmXGX3cFygrJRiokmyv9TIA4IUqxhNA9XwvJUjt9xXdUjHm/QCRc1OFHRpeZHCFI9dce1Ojdh8wn8fIncSLsiSDPXIoxcZYjKU11XSH07ppgEztbwCAcDwvHQ0UgIY21kFapFfuvZJa0RX44OhQmmAoE5pOOV5keIQdAPOBQEAwskAEMgBIDbyk68Ox4h6MgBfaaWAmTKI0CbOK5kNPv3wUzGEwwnBtAWJl8F6OFUpES0A+hQaRysvx2jx38Vt9XvagZBNyvxYHLv2JleyH93gexwAWvaekF9Sjdhx+QvoB5wwCVe7hYDRrT0gUnjaw0IwkEBQMEScnUPrGQM3M0Ur7z0Brs64isbGnjLQpoQnbUgPxWyCJ9+hmoGagoNEmDMaex91aohXhAGsHY9UPO86SWgIqYsyjizcAUoCpZDYPMK4K9NX8SKWWsEOUKzFDnZ115wnAChJu2PMA8B49fpzOJmdpMl3SXj56OuH1H53OE5pa3H8sDb/O4ICB/8tvvs6f8Duw3ruHB/x+vPD7xeRM63EuZpDL+smaYZmftCxVOrqi1iODcgIVKGFcpXv37qZ7d16iRhj2IcEWspHRxCu2794AqErAFH4xExwXaABm47YnUeF9gyYmAjhRottOAACRQP34o9+IARglwGbbu4kFxyD8HjC1prN1+slPfpygAYh7qwFAJGxx3y4Bfvcnn4iV1VVFxu9/7ztcB97HaEKB+wHTHaDdePKE8wJ/Cgylw96JHrFo5kX7E0xjM2D3NWfK4xd+n62HEkZgAYZ/yKZwSrAQHAuJEjGZl2nesnRKaF4aIFx1yTDCuNGOLtQtHZUheK4bN6+SQfjZbz6hH/Xy7W/xceA3KqnmBKhKmTHuZEzGvENzVQkvxXlYj5SomYy5Px7df6ThAWhEzbxeJJoEFp2eCUis15ETZrlTb9gNnMMlgNXtq8mJ2P+tdPkStAwPyUACCHbl6mHIXDXnvxNzGIfhuiUG71hdoM9m8oPQXZtddqMGVOOO59R+aEfzPST+ycxvDznOAO7iAIl9CBkBVHVEBUT4bwYawQAnGzuaszQE0OgqP50xcYUuv9Si7as77zHmcr5IRwcHav4zGBD4RLMHdhvv96X56WZVrrzLPpDsMZoDgiGN0ng+12qq83e6ICA3P1XzrhTNdGbUK0bzoaZyhZ8LBhkI8WBAr+bSQ4edUXyi5CKAWOxzAFNm5WMe+r2D9PDRo3R8fMpkMBhqYDDjq2EjpidP0ieffkoJFszPoKtzvhNMVCeKmvg0dlJL56F948y4ZzfwhnFqO7Ud3+o6ZVxcVmrkBJkTghVu0JS6xjdYA9DNH1KfGoeTs9Ng5a/TO++8TW1K+MbzWWgcmglv9llVAuymMX4O7yeXABtoQrd5rBMAgGR8dkVQYDUQx0NxqDTvxKCPBR0HxWYibp6b08nPMc5yEQDYjLcBgG28QvuoatqYD5R4f+wr/7o8k57pbIr4OxM9Kv8hazpauz26AOfbyPZof7Vb897y/75eCXB9fjgJ1/o3f/7vNwDA7GDGgrFx3QUA0nD70xarDEcgt6/Ov29ojxuB3DMAgNpBupC1JPJCrQb8twUAdTgiKtk9Ed8UAFgvqv8OAP72ACCZGtZY2aMBdFFJ8D8FANgEeym1e9E1dZ7SP/7oR+n/+D//moHAdCxth3arL5bdciYR/P6UjsXhYEKAcIEuY+wcJ804a0RgnbH5ABppofTEIq8BGExn6tIFjT4yk5CbRbDLjsD9rNPW7Q3St771rfTGW2/QcRqPT9QsY7BOjx89ZnewfDijRMqBQM5YNuUDZCWuohtZNNmQI7rZcRd7OwMJ4XDkQLbSgUMGiQd5MKagUaRAVhqD6HJIYCg0VNglDEFYCDND9FyOqjqo4dko+h+NGVjugmwqy2vBWhMIiPtmQO7ul84wZ4AmShoqAFAZ7OiY2Wql49NTOpDOtOPaFOWOkhU4jAr0N0GyBgyPpgnWbSMY2eYzsBtbMAZwTQE7m4BRFjUunJwSaMe6oGYdD3tkb1VmixdLAFfSwlohYx/ZaQpcs+kEnAF0VVa2Fesa68fOFBzPA4DcbIah+/bB5GRDqQFYHn8NECJxen43NewE4K6WAlogfl8e7v7//PnYH15vUUmeu8nBudV6bJhFpQ03oOExc2mtHSoAeCgNcIY0A10RkMOAUdx/tSJoawYLfZcluoLHHe9gSOE+GBwUts9zwxJknJkLaYv4/OoXJYx8LotU5fkX4APmRXayXFpdiK+7lBLrSudyAxDKHniNhDaVWRrxe2ti0jDJU9M4Q0MuGJBm0GItD/sDMmYAAFJLMYDkK92Ubty4mXrsZgkGlu6nD0ZWt5vm05l0myZgKgPIkkg7mE78nqSxz6XJabPJArS00JQI9wBG7Tz8IjAoyJx5Opb2YzBzaRcYzJfl041PYRzD66m1FgB6HgCo9be5jjNAXGXcnDg1AGhH3FpOKiETyIp1Nzg65D6GvUQiiiVrYLF11bjHrAyscwCFve6AJW220XNeq+mWCQYRnyefA15HTWI433v4W/i3m9qUe3xjv1fAX96De5tERGIkOwgOFJoAZGPfUK5MwLSfzU2kOJYLBTkuQca6IBDUF3NYY6NSPAAH6A6M8UWpPzX+Wqs0YjONLgNU7ZkWmyhgbZm5/ejpE5UZztf83KNjlBzO0/2nZ+nJk8fp+GxNjcDJAvIM3fTy669Te8yMvxm6/h6MBBJiHlHuC2bbGWyL2INOLsF2LKClOUPwLvuLUlCM7YBAZi8tVqe875s3rqS7d++mF1+4wXvH9fAqkym7kji5W7Qn8xmb7m2tg68JAA66BxzHD3/18V4AkPfPUsc57QbGBuMO6ZWzicBJawCy6QLZ1psA4K9++ZAA2KK1pD/25ju/RzAYiS8HwJRqWC3ZhXu1mgi4vfcybbcBwNwUMftCTTMp7eXNLo4eHydXkQAuA241l2gAQJzb3DNFZQaugV1BXxF/J/NZV87c/Jk0kTFGSICslgC8kLSEjlqP3aLh173/3s8JRLz++lvSepsLIG9e0ekeAKB1tKkRqNJhNSkhD0j2Y6XOvYMOurSLOS6pjehiS38XzE2dL2QXlzYiErxIgqLZDn6y9N3nUvh7s4U0GM3AQckvS+iXSsyPDuQLA/AlMGvd+5ZKqLvzqN4IvxoAIBLjT0/PeJ73+gM9b5R4+/kMvOOW6Q+ukDTopGGUIAM4JnOwK+CQzTpC8xGgwjoSTaPoEr1qASiEJqmabKD5CRP8U/huC5bG4jo9sI3PztKTMXzDBpgajQ5Y6m8JADyzKoRC89Pdf2JCqavIxLjGf7GUtiCa2uF5ByAngDF6KoYfmMwkHgQgDAY9EuKwMfZP8Lxo4jE+BYNfjHo8ORmDQWjANsD5hEQF/Ft8NwDAQf+QXX4fP37Kc/m569fT7Vu3ErTN8f7J6dP0yW9+k2YTNGNbpmFvJKCqZQkaAyrGKSxZIcYi7TUBLeMEwSAuzh/8PUuVuXQ7flHCU8ZNyooI70s3A/G+yRX5mfES56kTbC0Bx9PJeAsAZII1NACzFMYFAOAGNhPPwyYsaOyNLumLOQFyAKyY36vX0Cle7HE9VwN4siQ4538aBmA+CiBxsQcA3KVtbMtU3iMY2TrHzwcA/RnHUhkQqwDAPO6hp10z+LbOpd8RAGzyX3sqEvbKYD07AMhn33F+0v/4V3/2n9Ze2FrlcSModyAluRDC32jrXNYxF9IT1tRx6VsGBDeHzuvZXVj9Vy8gbzgb3fzpuotbnTk+R4OP17If7YW6gwHBwN7ApDMgnqmagfg1m3fUAOBmsK2jUCeStTPAvDsHHQ5NgmZ0d1NR9wNh+67tjHsTeJQLyc/RFPHtvg4ya+e/4gAt3sSAKv6N8eG8FX/HwjV1uBuaA3Qaskh/48j4Y7VhbjZ6lNjtuM9chscAeFMbIM9bAJD5/qr1AScFBhoOGYykATLqUq3X6fDqEYG6n7336/RXf/XX6aNfP07j8Wlqr1GasUjDtgCjQTqjtszVSx126+10o4vY6iwOYGuayOkCcY/AYFIpgeNsl/DN5qLCu7Sj21HGD+ueGoBtddN76eWbbPpxeDTUgRszc3o6CeaLDnocMAKtYiSKwJSsrKXmEYzGzdcmAIiPqYFJiBpbQ2zHuNIZo6YhNEL0/SxpDg1BfA+eAc+JQ4rOMnxO6mEpQ9bvCNASA2tO+5C19CKDTUcOJRJXr6bpbJpZgAYCbavywVZ0MXXiIpenRom2s9BgEYHxgTHEYQNhfgaSmR3hlaVMKDPVzCRrPXofsOQTh8VCJdF0iiFCH2LUGaDKpdfuUicHjXuM2mcCB7I9Rva10NDwsxqoy4BvfEClp/j+yNRCJ+dgxGdCQIbSGQjR42UtMVwLQRMBxbiP8mAsAfN8X3aweO8qyyubkdAeIOBlyapZkk63ROdPlsCGuY0vLCUmDEIKfNUbM8hpZh1KlJDxdobezUDyCGLfLwlAYV4GHTF6AUBjvXXSLDM0uW5DE9DP3LAgNacOFLNdY2DUZE7dBCaXYKDELbQD8RmWNyMgtWPGfaVyYjpwUXLlEqpsJ/ecQWAsGNDFe2sHklpL0VzEXa3LUm0ECrTxxT7XfaqE25oy0DI8OjpKhz2VlnZXYPq00qjdTq+//gYZCGQtrFXqi/MT3434TIChmC4McEotqdx8Q0/aag80z0sxNadzNU1AMw2Kv6OJUbeXpgsAMadpNllmjVMEwLArZA0TFFRiRePiNRgjGn4KAl2Pm0rJ9HLFA77Pe5fzH4kcggoEMOvEasMILgMXs4qpBsTSdpwfy9RlMxCUaEmrigBgEUgy0IPdKRihsp96H1SrGKj28TswURQwUUsQ82CGqpkLURpnPwcMZK+fsjQ07/OKQFauL+7PAhj33zbWoB3o7CepG6Lnpfa/SgAwb2FOSADToQ1qKQd2l8Y50ldyqLGzwfTphj0Nxgg0AwGoIKGOwBVNthQ86f2wEzj/UQqM53jydJ4+/vjj9GCCrsBfpEXrQGD0MjEIu/7Sbd1mBNYAZHlvbTFo6ANw/c/1fQmljT0y+fgxMGExHnFer6IhVNlEC5+/POqnN954I731hrptT86ecB+UXZh2ySSgeUr5wgmT57ZMXFRMwA2WtvfEDrYgAfw467jeIbkAYLsroAMNVCid0leiyfqLrHwAMBHrZ7ZUF1OI7f/whz9kN3Da694lJRDX8hf8jN0hgMNlmkzVRGWyXBGgPbxyVQlJMI2hYYazHEAsG07AV4IsRy8ND0bp9ssvp7s3wChlyyxpCyYx67I9jmY82Gfs0kwNNzDFNCh1Ey10GcX1eI4CsA6NYDyn9oUShllrLKHkE9UikdiLfbruSHet3UUn3n6anM7Tgwf306B3hXZwNZOdvPnyAe/7xz/+kQDQt96I9TbNzSqUnLNdC2Al/CACTHx+AwQqJYW0CbXzginnxLaBQDXLQCIn4oes2anEHwA9+UB4LgBs9t9RBRHsNXR9jwoLnEPwT558eZ/zCmCN11+DIdhNh4fDdHh0xG2mig0wf7vsEquJaHF+UdrKfbwU8xfjjrPUTTymMzDJT9OYrLxJWi1gCxpgcXAwSlevXEkAAAHALRYTvn9+NlapbAsMPjCHdS6MjlRqjGZATPK2usHQFOAHDVwdKAD1B5TIQKL/ZDxsfmI4AAAgAElEQVRmQhZae3iduas0yneL+zEjkRhBJNjYbALJIuyzNYA8SSKIybjiPoE5Y+LMJeMhtbHotHhenk2XjGOOjtRUyc1PMM+43mLaMOKt9we/vNtTHIFYBBVRaGYIvx/n9fExxlTA59HRQbp7725Cc0XYu/n0LP38/fdTd5WkAdiG7A+Y6mbIaZjMHG2aUDQgNs/zSHbhvfS7KoZbycj3ezYMYAXAed0bkGpKOuMcrxj+SFQqDo9EZy+aq5jx2+2nN994I129Ft3LQyKqF3a3YcAb1NzU8gezncslbhp2h1IVQaSANi3A4hdeuMnfX75yoP0e+xBcUI5JsHw9PnmcfB74Z6FZ6nVajleu7MrvD4Z/xCQmzPozLsX3v+uYoCZymVlbA2SOn7bnbjPR7YRCja88q9SZ8a6mUrHx37geLUVTxDD4PeI0/t3dhy0dEzfsZYP4TJWG+gMIQZxfE+p2AYCk2iLn8g0AgM6sb2mfxQ0ZAMyO0w6kciMA/C0BQHQx5IN/TQBwC6Czw5nFWXcDbvXCyQu0cmIuAgD3XSf//p8YALSGVFlaczHoVz7V+QCg32mgw+Plefy6AKA2VQlqbgOAFpzdGPsIFLcMQtxICQDW71EGGEGXADZvbIBTq846ffnll+mHf/+L9Hd/95/Tw8cKDAadyzSs3dWUjsbVYaLI9NGBNJxW0FRh6c4kA4BiskTH1goARKmHvlcgCxiA6kIGJwPOkjTP4HhQU6OzTrdv305v/t5r7G7X6YoJBgMLEAwlBQrcugnlcRAkD4sUGlM6WBiM4+UAcC1GXvMKhzAOU4yPAEMBkHDG+Oo0hy3u3+9pr9GtT7R+MiZ7KnGm7hedNN1HtiF2FOPk6EYJrNljUJlRcN3sa5YY9PsEAOHUlC+X1G7vU33eXXs5BDTWm/bCotL4Dgb4cPLwHNFEAgxDzZsCNSbNEaQhmCkBgwAAwSgpARYDdC4LrMttMQwG2SVmrRIdA2oo+9rF7Mj2Oh7ce8bjaOACzuDGeK2h3YLyVzWnYZBCUKpmOLj5hwBwM9tqewkm1wbAF2xJB5AqfdsGALkmWLdclQ8F0JwTrqHJ2DATAkCKh6JGUQCAnGOXrcfB7G6/EM+n4x7NUHrxuUEnNH6C8Yd1a30iXK9pwhEaTYD0ozMvb4FBaQMAEhQqRJ0BJJ8HAPb6zbhzbKvu1dvrevM36Jqo9SnHtAYBzmuig/tE8z0G5rlSYDORJCZANw1RZtnvpcvDLkX0By0wPRfpoNNNr7xyj02JoD2UOtHFEgEYHNdgSsD+KGA0M6bRwtJ9x3OFRiqWBj5/NkNHwiW7EML+wZxRy2ieGIRMx1FCGSXy7IDtgCgAwLJ8PJ+T2Y/YZJd6HDwKdRfvkvCH+673t5umaU6KUvJIGuSS/FhDOBdgZ6GFhUAKjBqWog36uRmS9lzl5wRzuwv9QIjsDwF+dhkIwz4t7XDGRgJTnGvEGkihTQjR+N8GAMxyJM8IAILpbTuq/5F/67XrVZ2ZqbZrTgwUlSol4D3q9aXRGowlBktcTzq3BsMOgZH+QM2F8L1M8ASC48QY7AvsIQBAAFeTkM2Yz7vp3XffTffPlpQIOZ2ri/ro6nPpxo0baRIMIq3rNTVvybQPjTvZTiT6VIJsRig0yLCvBn1LVQSjoBNaqDhjvJbBTOqm9OKLL6U3X7udrl9/Lq1XEzUvKrZrfU5ofDf3cwkA0nzFHNRJnn2/r+0RoBWzxHGGsBswEkodAYC//vWvuW93AYCyO7q/+WqhhgOrTvr7v/9hmi2wj1dp0FcgvViFJECsh94AzPNlevT4TJp/nU46OT5Ow0uX6Y+QjQxgOBjJabkgyG7AfzKbkVl5qfdQXYW7ASSH5qsBwIOjq1mjE34BgGZqdgJUBKMrEixmwSNhQoAwSlCh0cZX0eWUz92O+V4FgLrSOQBAGi8wa7iOE54FTR4SAc5e55I0MJcHtPUHV5Co7aYPPviAz3zvFYAuLTKPwMRCokD7QcCbAQ43MVgGWAnGDzWn19BEXhIQlOSAE2+hCVtJCgAALIGYhqEVCbMgcDTMaGtlx7zH8deHVuxsRgDw7GxCBjleM5SUt1ppOOqzxBtdd5lQYld4VSCZOUy/1P5ZRwnNObUpxRzDi1IAOEeoRbdIn/z68caZCUAUia6jEcYXfrq6hJ8dq1kMnoMA8xo2pMt5YvMfaj/jb0r4MTkOOzyZUWIAzEImwgEwo/JkfBoAoNbHNLo/ozTXcitMJga7PTd9Ch8HpaCwdQMy6JcEzpDAhj/NBn/UDpfWKBOO/RG/Z9XV/U8XiefM0SUB81lawgB0aEOyqCkY60wYU4tRVTrwk04jmYxKJjJTJzin55ynW7dvJ0j80C6t5umXv/wVtQWlKTxQHNgJCZJ8AETixv82ESe6OMMB35WoMsOurCC0T1TbLC2EYMrZPkZFUdNE0IFiYz+ZuLOf4pJ/MAB7vbRE0m2xYALvtddeTdefv8LnW04lMdDL/kYDrHI95UqPzQSz420AgNi3JQAI6YLnX7ip6xKQxT0K6O4yhlHX4XJf1gCg/ZyG8OWmdJujZYDLv8WJUkqRbQGutYRKjbfUihRmAFa4k33ZujqtPOPw3HUFbF5Ge3hP+6TuzgMAdyXWMqHpAgAwExxC2mgbAPzz/0AGYBPQqZTK2hNNlzktxPoB9tW8mylkALDWYiu/r5xyA0plBvBZAUAi4ztEHXWQNSUo5ffVjAoaqfPaUNcaJr8jA3DbOFQMwD3aOPlzz1hSUQOf2b6FY7zTSFW/VObBor0R+BUU3zynRpfP1XjZzRisEXoH1OXG8vfgb70C2OX9RXfL8zbieQBgrXO4TS3WlfVdMuT5//Hvaj7MrsrvCUAMQQACpwdPT9MvP/gg/fBH76df/OL9tJgN6Ii0kzKQaXaaDkYH6caVcEB6c5VkrqTtA20jBrZzlUQsYqLNAFygZDNKMmnMosQEGi34PdhtM7DPoLoVf8N1rl+/kr79e99Ot27dYCZyNhuT3YIMLECKxUzPj0AZB641jvCTGkhdlWY2mRWva5cEFsFpdC9lKRrLR1S2UM4TAhqDfg6o2fyj1ef7dRipjIf3FVpG+eAIG1cHKWAO1VkWjmuMBcYW15dGyqWsCej5zG3vzWAKgwyHV6XZIVIdB0LZlZf3SUfKzUHgUEp/EIeo1o4ZE8rcW6Mul8oWoBNLTYNpWR+YPowNHjrgNPBmnRV3WXPmTIFMdB4F0OWuonaEWBoXelsEJQMIinFwWTWAD6x3lnwAeIZzuFAZust/fc/SqdLY8D4DECYjrC41ZTMU2RLrlREADeBuHWvDc2pNEjq51BQKtkRwr/JZaE2WShwZmrTli00lEIRFF2HrCbLUG5+FA421sRKwPxmPpdXFoA9gS5SWRKCCc1cMSgGTTQn0bg1CN/3JotMBHPsemy7Eca4FkKyupAoWn/W1i5Vzkb3esI3FF3lvu/QzA17tKH0zY3ihQHDQUSOSawAADw/TqKcmOdeORun568+nyfRE67SnUnnbCATuKiVyYNs4phvOXTjUeD81QCOQmi6VaJHODboMN1pMmM/ZrMnEln5KZu1n5o0eHoydMjBAokXnSazDyOQ2Ca+m6QreZ8DP64P7oWBZwV6UY06WVjAbuH+iOY6nYgwWKsrhBkPaOAc4AAQBuGK/ak80QDG/O+wAmiugtLGPZhgI/M0+7gbg1RYTqgzweL8oqWTJfyOHUNr7Osgy85FjVZX9luNuBoOfL2uA1kzAAPoz0FQ57B7DzALm9zY+C0p5YbeGKJWjJppE6VE6qLnRmQRMFMz90YEYM5qfFRl4mBsAT1wXkaADqAb7YJt2Mlmlf/yHf0yTpAB61j4gAN4aHvDfT9HcBoyZDABK+xdaXryPsI8YF84DgXH0vla3ezMBF8EQWRGIarP0zyVclNRYL2ivbr90jU1BXnjuMq8PX8Vni88S+s+hm+umSJ6PzNar8OTatuxife+yUzUAiPOT495qk2H1ySef0Q9xibUZgChpIxAWz429jfU/X7TSj370jylwhR0AoK4PJhWZwVMlKifrdXr06HG6cv262Pxt7TusWzLbF2IiEXhG0obMpH7qTyaqSIgmbZ0lKitwcwueZwB0VZrvkkQBSiVTSX6TzqEMpjMR1E43brwogKUnmRN07zXjm+M06oV0jK7fo74hGIaWHnFCZZFOTk7TcmXm0YEAyLjuw/v3uQ6ev/FidG5VxcWq1gIOQBogXwkQiFwF/0JNaqh9C1ZdMFvlt6L5iFaBGVNOaONv9ZrB71zuDu1N7d9gxLhZXgcSD4s06g+4P08fPGKpc68DaZNumsxROr8iwxnABzQCwYiEhi8TwxF/5Pg2EuwzcKCXqEqx/QZQ6vlBkzGN76o1iAqVlbrJj5VIZxk5uuyy+3zoRBPda3F8AJyQnT1o046su+j43pxtPle7C51XTOyjucegz3Xw4PH96GqMfd3LzZ8WcFWCvcXrR4LOQAuek0kOdiVGNKCKm5xYiAQZ/B7bRNiT/uGI6xHaiGTqx/kBxj7mrdtG1RHmP5oErpT8R5m51nQk7OI8tX2FZAHjhQWYfXMyTeFbwlaBIQ0AmyXJ3cQmIPMJmPyYb2mEr1uKo/zCc5CRWgFFasrFDmoVALgZx9aA1HkgYEk0aTTabBh93QIADCBdG8B4hs5tAIDcN51uunfvlXTz5nN620Kl3p2ERin4jCUwmsStn537sWiaiN8bADRwbwbgizdf4jpariYxP9IWBQBIu2eGWoFfaLz1bU0M74O3Gsdi/HFfno8cdxkfrZszbszmZkUKv7ee130Vo7QPYu/vAuDK9YL//+0ZgLrS1nl3YeWk4wHNY42v5fVrXymILwAAMfZgPnM8/lUFAMLw8g9uw5xH7LcDAL1wrNXlyc/O3moz0K+7EprpUu7QDUexBplCe8zvbwLlMPxFF7tyoZbrZhcA2DjVjeaWBmq3Nke1DvnPfcHQ5ns3AcD8HHuAwLpL867v5QJrBnDzLVsMwn1XiPv/GgDg/itt3lH5vosCSpdGeTwBAJYU12cBADfvq+oOWMyT1lnjqZbAY55Pi6kWJeLl9QGamTVGB8xMgnAGPvn8PrX/fvreR+ya1UqH0lqZq8FBP83TweFBunllEOLEp3Ks1uh21UkgveCgXC07cvxhuKC1EaViAAA3RjsYQ8tlU/oKsV5nxCAYfO3qtfTt338j3b59Ky2XyLTN0nI9UwY3RUfQdVelEFHqKW04ZDUj+x0AIDSj8CJZkYGjDx7rSEUJYpQQwEnC8yCYKkuwAQByv4ItRuAEgWUnPX50woAGL2ovRcZdJbHx7AEQ7TTkraJUJAKXMiFi/RHMHRiYGGPco6/t7r/ZzoRjg68sA0Z1l1SGtn7h9xKUb1ELUM0gnKF3F2IzAfXpvBYDMHDZDwDA0mYwYxZltf69AgUFaRn0jWena2ednADbGDCUrMNS9yR09xpnvhFsJjAdwddg0IvOhXKI3U0Pei/sThdMMnwPtbPQpXoGNkfD0GP3Us57OD9R4oRADC84+1g7AKzh7PJ3VYKmBAC5L4JpY6am15yBiRLs0JwXlhRd/1bSL3QJnu2C7TKgJ9p9AL2rZZqMzyjKvZiqRAelX2R9AlDodsjooJZO2Ac40QqaVRZfzi2/K5eURwmvu1AHQ9SOWL6PKAEzAMhGLhUrulyf9ZlVnr3cc7lEwUDW5s/NZMu2WLsZhJlxGYAYStnx3b1wbBFOgzFzbYSyMDACW1w3t65fIzNjfHasLq09AU92vM3csfRA1gItWIt8XpQesQNqiwHZPGwGGBPUUoqxB3BgJi9AAyRquIcABsBOBgPM4H8j3q5xyQwVnzNx/nqcfH8GdKyxlPduBGT537EX/O9a09EMUnbHDACQ8x0O5tk8Sg9HB+lgNOI65rOifCySVLB3HTYBCRZOacRaYiT1hrLzALQIDgIAhDYeuq8GAKgAPxiroZGmmMpNSyrbVSRVSryoBAA3QNeqRL4sEU7W6ilKgV3mXgYmeRxrbcGiwzjXvQF7d+eOMcG+0oZSEmIBUXxoHHbXYljGedwHAw/gamhHYh+5QQds5mIuB//x8TS99/P30rx9mIajUVr1LxMcOJ4tqGuGUmDa9/jeGRhGWIuhi2ptQAR21LBF10/MeTyfGWqoRMA6XpoZFgE3mIJYF722mulcGrUIAL76yrfoj5DpE2V+WB+wXTlIo2++mTAxAGjGSF7HlX97HgC4QQ7IpZ3RdIoNTARcQKP4/v2HGwAgAFbMB5g0WJfwK3A9A4Bg9oJxeYqBBYOzYgA6wYtmLzhrzsbQqlukMZpcnY7T3ddey4w3ViZEgwWXSAIQJVATgeflGGcAgEzILN01Vwwzd0s1QaXdFpCbz7F5M74CwSJOC7tMLT6wBTsC9tDkrfRvJlyfOHN0rnadCFvr+w8OoX+HEtgjla5HIgX2FEx+rGuOJ0BwlM7yfdBXE9DZG0Tixf4mQAYAhwHsuBmNxxUAOoEzNCUBgw46dCxZrjSVM2MngEEmUTc7AssGx3jkje3Ec5wRPfkZAPThX0yenKTHjx8RyOd+QaIJifW1mOjoCowEFEvrIO+Dvj747mDwIgFKYC/OsVwBUzFh3Sys3T/iWHXaam7ixDpsAyt9kGyPMeExtQToruZBeGH+6Md02tRghC2GnRgwcdNO44fYAw9orwzsIx759ItPef/PXbvB+KIT84QaH+9n2kUkvuhTQYt0FlIYeJf8ZpQkl82AerEcsd4BxKF8GfaoNxqqRLoNRvSIo8HEfUcJbzBPuR8X8pfaK2gPLtPBwSU9XzCs0eSH9pcaq11qlsIuodeaEg8dNeVZrdKVq1fSdDrmdS4dDel3jY/P+G8AgEzWBgCYbVZuJlh6Qfj/WDcBpOTlFPYt+zlVhWB5ldqX2qg0q+JqA3Vk9BfnIJm0QcQhkLPospS6BABffvnl9NKtG7RrBgD3MQDrp3QzokZ7UO8oAUB2h3/xNhNbZ9OTSOBL8qTj5m87AEBeaAvYqgHPzWYg9uPKBKAus5tit4XbFr6KAM7NCsBsdzbKuZsEw3ngH+8jvvB3AQB3nXWKFzdlxzbnahMArL//PACQfto2ABgBpjeAAUD3+YqFXWtOwEF2RleOVANg8UByaU8EKtJEaBiHyMxsAHoVFXOrFKAoAeaEVlD9NoNrcwGzRrwAdkoRfA9wDQBubtx/PgAg7+siBqAzBXsoqXWJxr7rlV1kNB6mMO9G8LNjtxfJDoCj1n6x/1wtfs+BtWl4GFaalDJUDVNmy7jt/MXm+ssGtgqK68A3r4live6aD2tC4avpGEB3CSVAAXR9+NlJ+tsf/G167/0P08OHj9KweyDx3bkyv4dtiXpfvyQR9tXylA5Bp6PSCzON4AeScg/mYFDF+bOuUHcQFxkOBJrQ0gBCZy2ie/fupddfvUvH7/jkAUeNHc3m83Q2kyPU6QwYGMPhR2CDn3AQpjN0TeywGyLGA46l2FbSz3ETCgOB3U7otJCZoCxpBtcK/U0zIpDBhvNIB6nbSQ8fPskBh7vdyhFujgKCOEUgUs4lHAv9vflMBoFQ+jGUSDXWBQIvOEd4frIPi+C7BjpKALDUUzMTOR8syHQzQFTXODhlFE8O5lp+X97nmxp9OFAFMAVrzKLDOw4POsNkIDSAE5w7687RLjPYR5Z2wJ8CeRvgS1naBsjRwQx21OaB5ffAOUPXMIjKe4zJ8oSI8/gsTZfI1ja6kGRD0BGUuLNLgBrgN0oJC92nssQX/49MsEAbaQCWL9+/Sgka5mXjqAtIdJDkfY5AsQxss32LktkGwNlkYCmjjHIcfQIAIEp63JQizQFyBiCLvYTuoexIrfGEhpTHzSyxDQAEIVvJWDf72kwJl9bnWoBGc00MrEj4RYDVjFWArBFAZa21SvOm6fK2bVy173cg3hvJsMYB5Jhb3Cqa9UCbCXZn2O6mw4ODdLmvhjzDtuzjSy88T4b0ag1GKfS/wP7VM8r+iDkF0XFtZAMUcf64Oy5K4ZjAEJMiMxQW0OicsOsg2SS55BlSA9DHEhPGzC0wQ2CryBpkaVSjvcUzoGLHd1pg9OC6m6VkLNvkPmsObmlp6b7BIMWecIVFCZQTQCbDpDH+CIzKwN/nNxiNtD+DPscXgBGbGkRpPs4Aaj6BsUa2aJz7AfaAIUD9zuhC6e7KZpC5K3A7EkEuxbbWpEpxm/3mVeT95J/41l1rqdy3/qz2Q4DlZvJGAsmJ7azNGImZevU2JUqybwKuNpkInJ852F6ypxusRbOi22DkQep8rmZSbQFP3a66RqO5DYEzd/5GQgUB91Kak4+P5+xuOU7SXJy2D1ha+dUTdFRfpJmbAEWnTGj9shESdZmKhidk9WCs9TxIQLBU1fILfYEy8K8oJRJLB/eHJk7Q4CQwkyYsPX7j9W/xJxhjYAHiDMFcsQy3TpRUWn+lvS7H/ZnLfovr4VlZohpNL2BvWZK7WpHx8+SxEgNYvwCScK4QsOL6Dlkg7jN1Y57OF+n993+eHj8RsLIPAOwNocW2TD999+fcpygBxr777h//scYxtIzBLKNdOUN31HYaHQ55Hr5w4zrP18sDfY+11lHCjLML3W0xrl+hyzgY89CrmxPlkL9CgHeZllFKj0QXxx9Xoo6skm/Ucy6kAJQwas49sN/wUtfNdupZA5nqEuiCPhOIhsQcmg3Mg7HYV4l0u3PGfdnrosEJgE1pYa7Xap4BgObw8CBdvnzERE2PGqFYYzhPwZDbrKDpBkO6T/sCkNzSDdr/SGjTtsfcwT/IrPvC57Nf4oqLbA6KBWfAm0w+gK/dXpqNT9Kjh4/UeI/MSTGA3LQDwBrm7/JolBOVALYMbMyXkqPB/dFn2cEP4flCPdk1GZhI8qV1dIvOkvvByOxLo5FzzUoIMfSQeMR+QcIQPqkTNjg/mSDryKd6/NX9dP/+/TRoCRzEDQFUfXJyTEZju3/IeemOtGdQUs2qlLAdWGdk83rc7YfmZjKan9y9Nc4TMBRxX5/++jdkNmL9EPTttMVYQ+SyXKbxFE20sBcHasK3AAMfSSRpcB6MDuM8BdseY6vEaaenZDD8KHQNxrktv13nFxKmR4dHTMBQq+7SIe/j9OkxrzuMLsCQUuL+q3WuC7fFuEZ5fuZj2d27K41yXnMPgFPiCdixZTydK6acqKrieyd8zcifzaQJv0xgJC84vrDL37qDJkOdtJqf8vmgNX1hQwu+s5JsQHKzKN0HAxDzePPmSyxVR+JVXbTlt0IDkM9edL0tbXwZv5d+meOzrRLZSKDXAGD2D6qDG99eA610+yopjxKw3eXX20/cBQCWFWMmGFiTz7fzrBqAsOfnviK+K6VdSj/HhLks9RnuYpYa8+XDXhr4w/nOffuv/+I/8i3uEuWBzu3KfQGXTBT6XQxMAtCzdlANAJqa2IhcRsmMDXj1/HUJsB15lxXw+0r0d2uD7B5QO+7/tQHAXYvxghnXn3d1bdnQrmsyzede7xsGAJvneTYAkM5FEfQ0m7FhaJX3v0W9jj/uAgBtuMvPA5AuXzUwsz1WsR6zhtUmUmoasD+3db16nvYAsgzWeLCtWTKFQxrd/H78iy/SD37wg/SrDz/jv0cDac6Aug+q/KVeS1oxI3cbOVWmq60gF10GccBBpJoHOLtYAszSc+wDAL2+sD8RiOBgxOvW7ZfSrVu30uGoJyBqcarAB07dapVOJxJ37nUVWOOFQByAHJyayUSU89QNbYisoSGxYgeQmeofDKjmYBNIqnWjw1EOWHR5ddeoYDjtK9Hm5+HoAwyIJg4GK8s5PDs7zd/n5/Faw5yxwyKvo3Xi7opce4X+2XYJUwQXaAhgtgg1sCpEFqVaDuopiK2yKt+3S4ibTJUBnLhOHBAqCVKJiyQc9JKTHuVDEZhhHJxh5jPRIV3QgcCygVZRvzfkvALAU/ljI7aNzxjgs6Pt9ebv9FhiTQJERRc9duGLLscGADfF5vVs7h4MFp8TPE0n5qI7rZ2v+DIzlAhEE0SdpX44wnmxNjeWgV/M9y4AUHMsIMkM1Po66DpdrhvYn9KhcGkMgCmMxdnJaTomA1AammZcmLlsQN8MRDBwxNBoWIHc3y4TDo27LJbsjtHu+h3P4PPcAIj3QCdra26KGzf7dN85E3azAho5ZlU5fD1m5ZnIhhzhf8SmF1AV9z9C0IDAGYFlu52OurA3fQKAeN29/VKUSy25VjFu5R7tdQ45H/PMjHSjhrh/MJXRXTECL9hRJ1CoAXh2qm7pwaACwqh9rz0FCQLcr4M6MH4IsOCauGiUU21o8xXj0++5CYOSoq6UkN1rEm3ac2p2g5cYYwBb8oLeSGKYaehyv+0uom4CJMAG2qkEC9y0pSiJNwhIRkxbXWTNxEXXX2qWRrftrE0TTCM1CUF0ICYQutprjweT14nlAqwswb8MAAaQft5aKtdVCQDKLkZgkxkbwVTdkxgtAUCDf5zvCEhoM2BbVyqtKoM9lZCFJlJXtsXdYwG0YBz7A403SkPtHxEwYAl/mwAg7Nj9xxOux5Po8jrrHHI9fvzZAzGdkajBmoRUA7sqBmsApXy8RzE810toXy0zAxWMLXS1BmDN5xqqOYm7aLty4AigMErwOefQAlwSALl398V0585dlq5SI5NBt30pNWHQvg4/MZ/XjabXlk3+LX6B78T6wxiRkRsAIOYHAODjR095vrPxEvyhcwBAlFZPZov0y1/+Mt1/IO2siwDA/+e//KMYaqju6HTSH/7Lfympi274YfNo2oEEGc7dgRizL798i+8bJACUoBWr6ykAHZzXcDfoB1nTOZgr6otKKhjPgCkrPiRlgeug6zmayDx9cpLG4zHXEN8eCU40S6DEQQCmHnIBiljP4Xt5Limpg2YiSgSMp6dKPKMLLsEh+XvowooX4ixKmi7frgoAACAASURBVCRICPTpT7jpEgPOqDyw9rD9Gp/dBgDB/PbZq4UkYLE7HHCdDkbDSAJHc7lC4oVAqGOlrGkroLGML2hv2/ItkWgicLuYURtPje1SmsxUaYP7xDpHyfzh4WG6fDDi8wFoNTOO1QsDMAm7LMGnfnURr5YJ4yxn01NTivVKdhVSFSrpFqgNoJSAn5PUuZImlkEwriHBQC04cDTRMGsgDcHx46fp8ZPHqCinf45zDEDy8ekp19mlqzcI5ECLBLaChJ5CG7Xpxqp4bRldnK1l2e0MleC0xvhC8Tn8Iqyz4ydPqZULywC/gF3IAdjH+sJ4yXcOPyoqi6DlB9sxQfM0JtQAmCqBTUA7SsbPTsdMqK6SrrOI5iuYMgCb7Y4YjKNhn/vh6WN1fR71FL/MlwLItrTmCqYYmXS1lnUwtLBv+PkqcL047gwAJppr5USf48kAALOERSTe3fxGJckoB9c5gkQAtTm7HXZzfu21V7QeZrJj3damdvl+U7sJAKbQCHTp/dlcADyagOAcmE5PBcgGAIgSYCazDABWX2RpE/8azMWYAP6o4/8MLFf+rD9fHt+MSfac5/sAwLpignPJSoXdmtZOpuQYsWAAlmDhfy0AEJljAdQakdJv4XgW/gDXewx3AQD++wAAw5AWFFgGzsFQsaF2NyNPwKJdiCmX3S/DQY64ef96i780jt5mgLxxCBAXqxHDasFWmWJvLKgVaIU1ndoUaGxvjF0lwPgoD7Di+3lv52gAfn3wT1PGVyE+eu51asBpB3BYLoztiajGb8f1+Nw50NtHJWyAtPo7zgMAzdTyVUuH39cR0Nxc1Qu+BDqa935dADDsTwYAt4Xsy+cxg833aYA7L4sakI7SDjobcLa7HTItQEf/5a9+lf72Hz5JP3vvvTQ+mdKhH3WGPJi70ZXu+qhLqvWwoyz7Op2JiQLHBUyB0OBh4Aq2SUuMNScSm0oJD6AAIq8z7AscIGDPXLt2Ld15+RYzoYvlmUowk8pRXbo0Y2kemHzRfSw0/5hdRDexCSj3EHtWk4VeT2K7KBkhsyAcmG4HzId2mk+Ps26ftUSkiyemRAZ+7fdu6F1BZ2STJo39WTK4XLqH89sgkucOjup4ciqACw5wmUFOyjCC/YDPOWOLQxYOFsawbHO/dlfO7DxFN2bocpRMQRSlFoA4/kS2UOjV4HnhfGYAMLqEZoDImSzrtsYB3QCAYea8X4Nh3Tgk0ZXOJSHstKfOpViTYB7QgUPZUKsdHStLm1tpmJFRJefQcg3WWWKgH4Ce/Sf/DsA1S52ZYXS3PrPRdADzMCbjO4LIYAf6TKBDb3Pp5hsx/tPpnBnfXnxxBmtjMxt0ZewVwTytb2xkA5Pld9kOlGcSgBiDNXTSw9FlME99trAvJGkKpMI4z84UnEFEW9qGDtIQDIjZje8hiyxKxOFYoeSGc1kldkrQkc9h3C7WWlmaWNozdtvdaIZQ7LmKyaMJaVho22eJgtxsi6Mcr7TjPNoC8Ob8LaNkKhKJLo3zeTMAYISSsfhMf42Stm4aRQLkf/yDtwMUg4bSOHXC/qJklXan1SNAs2iJeSLtuYZBAnF8MKnQBRP3Qy0iACnsArxIT46fhN2FlpcYcLxOZsIGxYMi3isGErRzYFKE2Dsdw4IpoDEI4LMTDGRLBxTAG4c7GF4OkN11TqxRAIaxT/L5FUzVrMGp586lKsEccRWGro9uiC1pJQZlJVduxPmF9dwAgQADdF0wIKkVBepQOKAMxLwMgoHbaonh0mprPtF1XusumoAUdt1MhTKI2tLuiUXl0td9fwcQp8DRpfRhY7KokNaDX1vN6mxX3H25ACIFKBhY1InbBAZhJ93UIU7kFrpkBtBDkX6wWrDPwk7jHDVDBvbhyVhsnK/GAgRnLWn/ffZYIMUi1jkiYvpJkTBwt1SwZmgPoqlCtxOlybOJGIQzlU4isQ4GXGcQcgzRbZ4NBnBWrOa0ZwfdNhOYL714Nd25cyddvzLi8xBkh1xBxAtcU1XnwtpebMn77DIoF/6uaYgl87SIioRugrTJ559/wbPdAKAZr0joya4CFNTcAQCczpfpo48+Sp9/8Zi/awDAJKDRpboDMeB++u6vBLyR0bRIf/wv/1RSDvQb2mk+nshukNGJ8VHThrd+/y0G6qPWA4KnKG1kYhVnHEt2o5mbA8y0uY8zk3V4IKBpBXbUGoIE2u9RklqXguVzNezDyam6Jre6am6FLrUoLT8ZRxJthuTnIp2dTakVfTZ+zARhax3MqYESrcugjK7XWp+5a280l3Oc1Wop8WUPdZ2mYhAFE6qLElB33EaX24G6jAKagn2VhlWbeBG1dw3AuPkVmc7NGXT56LK6lEdTli6lDCS5QmC7iyoDABZK0GH9sEkZwOR2J80WU84fbSOADiego0s83gfAkB111+t0dOmQwEivp3NBZ5DOC84LvjeaXuE5AVSyudRS525rhQYuaiAjLT4Bcqt22JfQ9FzM1agNwLMYcTjndK5duXI1HR0cUQPz4RcP6NsvZ2riR4o8gE10GJ4vUh96upAWAOiMknX0xcD3h7QP1pFLfOWXKIlrSRv46rzvFFUcoXuIbtQkDIyVQHZXZjAqwepEcxpLuQhItcTPiGXDJ8eniguCse+SSPuR1ro9fvA0HZ8AANT4obswrjUY9tK1a89RIkn+qLoEHz98xPtHyTXnZrXdBdjz5p+arzhvLfHgMlQ8BwUKt+VZLjRdSL63lPTP8b79gti4q5X3iw88++KyB/OlSqvd5BHrAAD5W2+9zp/zyVM9bzSFyveZgcbN+N/nWT7/A49AIg/XmYQ0y/PXX+A6d3Mtn+NghFMyhJJQu9ERrCNffxcAuBGH1ZInewhjltwoAUAlHKJCKoav9BP4t6IHguzSZuLB9rIkAtkvKRMKah7WjGXjP5WdorcBzosYgGQ4l9VsxaKir5ERv8b/4nkf47QPAHS38ta//ouLAUBuhLhiXQJcA4A1pdUO6rNsBt34NgBYOlbPAgCW32W/+5sAAGXRNzf6PgDwWcC/enHqvrcBwPL5HWjnZ/wdAcBao2WriUoczLsAwPIZDRB5c5TlFV8HAMxGt9joDNANfJg94Q1dbogInMv5L4OIZ5kTBfPlHNeMrc3OiigNKzecM1S+BxxyLAkIkWdoJeHAQ5b5+9//fnr/12fp888/ZwYVDn53LUfjsNsmhf1SV2LRvbUAA2hWsMSlpVJRlWgiDJLDuFqH5grAMwSVoe0DZofWkUs4dYfeFzDmr7zySjo6gFbGKQ2jgD5lkJWJRGY3SjiWEp3tdsVYYBkEM+xiCALo1BfG965QIjxPcFzw6vUO1L0sHEI8GwXFc0MHlJC4sxm+V4awtP+YK5Qi00GMbsEGAL1n4KxqTsWEcBYW17KDhfsykJod/I5YjWbL0TmKbrS4Lwk8d1PZBt7Xl9MXAKC78HlBREbae6LTgfNsxqM6C1LTJNhyzX5sGkN4j5QAAB22stw+HIkMBFWlmx4fZIQBSONap+NxOhnLISp1+ezElo6O91UnMph+PGeq5WQ19S9dOH5kaqxywAMHYbbY7OKrtQZWpB3mJjjnfTDb2OzBsjsvsupw1OFkz6ZqMJJF4Q0Exo2WAKC75nEfFwCgAMYG4PO4e+y871XqGjYjmDLW9INd5PwWVH+Ieh8/fsTMdWe5Dv0/gaXU2gzgbQMAD2cG4106sHD4a7umAFAgUy45Zflf46jWZfZ6Nozrps6bmTye37JExA5W87ft/zOzcp9miwFA2CmyeILBjcAIz9Fbd2UXl2LS9MIOHXQRsI/SH3/vD1Qq3mkRABxGYI5zFPYJABUBio7sIkqn+IqAbN2OjoJTATadzlCNiAIAfPDoAce305fmaM7Uh2PZbqOzJ1DkKMFjMIyEwkyswGnMZzDPPJ60Sat1Oj1TF0iX4juwso6hATo344Fmov/GdZjMDPWecDe/zbnICYLo3quyHZUQi8EXGlvsFqySZNyXASTZbLHEcB7pHqA7NSLToNNXQO1S5hoARMTO9dUWwwVqYwa+va/LO67LlfYBfFlUfE8lDQJ6zru7mkZAnZk57sYY9nFLyyf2tQEC2xPvFwOMaF5Q7jczPy0x0WkJyO8PguEMRh4SZG2Vui1R3gnAaTBgaR7Oa6zrk1mL58yjKbpcjtODE13n4dlEzUK6Wn8z6OZCDD8CVSTeyegJ5jyaGlCrsSsmEEo7CXh3R2JLr2YqYx5IRxdAMJloiwVL7HuRcOwFs+/ypS5Lzf6H7/0LrgncU1kKXJZJlYDM5hxH0BK+XQ0YbrPqtycZpe11F2B8R7criRJ0Aa4BQPwdGsglAIixALMLACA+8+lnDzUfoQGIyiZXJGC9SuJklX74X36m/Rtu4v/8v/yvAYgHIAut1+hCCrsA/wT7581vv8nS2M78Mw1JMJvcJAgBN145kRvdyd20CvOJ8/Uk/CUwbnBfKGWl9l4nmhUEAyMn2CMBmQPD3BzJUighn4DESbuVxqdLfs9sptJQJGyxHhezEwFmBADb6eREGnEpHUaiRfIhs5kY0cv5hJ9fL8/EbFtLV7XdiSdcSUYB/3RgzXPMia5wJebu2gwNuhUSNvWZE1rvblTkeXazpLKrKwGgE9ovnM9YJ7ieQXgA4ij11nkrxtViOkknp6cEkp4+Pc4AA6twKCkSz5MTUaGLSdAxGiNhXsmc1b5nTIcEOc74pbrdGwC021YCgBhPN/EjwsVEkeb98PJRunf3Xrpy6SoB8NPHJ6rAAPDZaqXpQkxGNgOBT9TrEQDE+qVsCrFKdM3VOUKt4ABItT+VYML9l/7RBgDIBIf87i+/+Eqlx0nnLABA2H8AgPYroTFqZvugf4l+0dOnJ/rcDgCQiQ4k2JarNDkeM6GKcwz29HQsHd7Do4P0wgvPs/s6Gf3LOcfjyX2d59j/eB0MNgktBj5l35vkmoHTksHG8yt89zJu3ufrlLGoV+3c/nM8Z00kMXM8r/LsRyoOWyb57pB+4DoNHchvf/sNzisAQNpDV8bl0u1N5p2vfxEAOF8pRrn+3PNMYKCJmeMr+ggBAO5jAPp7cpfhigFYA2J1/FLyv7j+PH6huZu1pAuixAZOURwhNQCoOQ8/yKBbpfFfgn94v+9nXwmwvzvjIvURdkEJ8C4A0LjRswKAnJ9Y5os4FywV3/rf/uwveUs1ddIHRO6Sa2p4pQ04d6AfM1t3m60piVvmeg9jze8r0U8OeMUA3AIErVETGdsMQBaMCQ6gxXejKUEpBlneo4Gt/Lu6y3BkPraANFNJtwhzBkSCuRRisbmEK1dc6X+8wDPFM25kH9W1Ht88jvk+thl/GwFkMR+bi73qdhsXhqNRft4lXdmgBChRU39rg7YPnMtBcNWVcwPs2PHQ9UY1y2jDsOPeHPBHGaBLGnPAEQFT/orIjPvfYnwgoxFAmUvDkph43baaOrR6KE9rp/7wiMDEBx89Sn/zN3+TfvXrNUsCu8sZmXfo+gvH6Mqox6YTlw9CI2J5Rme81Z5LTDcYJsjUls8EkV08e9aq6kC7Z5nAUEMQCWCF3XnjhaDhxZs3WfqL75POBgALiYk7c5eBHTpKhfB0zrY2mlqb06EDGHGUgJIAdgJoQIlj1sxiKbPLRKU5BSeAZagIVlB6UrAKqHnRDm25aPrBrqwsOdOCf/zkkQ78AAhdiukuiAIwo8yT5XsCQrwOhsPDjaYC+IPBTn5BdAdbLgBCaG9bBJtrOg52d2iTJpYAQgZg4QAzqEDpyDK08aYQfwbbUMCu78fde+1krPn8YnXgJeCkYeVhffL7gtljgNPf8/+S96ZNcmVXcuCNPXIDEglUYSNrI4tskmqym0trGemDzNQfxmxsft1Mq9Wt0a8YM8mkGY11j77MWC/qJtVcirWgWIXaACSA3GKPMXc/ft99NyKRqGpK+jBBo6EyM+LFe3c59xw/fvywq1sAbmR6ROmeQTaCvnEYSmNF5eBmuCHTZ5Cn3HM1AMihQiDA7rMR+JERqnXj/V9JguZnwUfESlS5JZ81wLkyKWImJcSzsW4ggs7sfTEmHM+4h9msAe/07FF6uaWUn8/UKgFcpk5PGn0GdvLYZOwzytcjIFGTk346O7tIT58+TfPTc34ezA+sdzTdIZAZWmEOKGpgjndC+6WxtAaUnWqsRAVQ2o2tMsYCQG0zqzeiqQxwO8O6kSB6Qad3fD9K7spXbefd5MLNx1irlEvWVglNhMSU1HPCX8fc7XRSuv3qq+l7b7/GfTedqlNiDwwGammKgTwcApzAeR+BUk6Nat9PpypNRzyAwAeOHNYKxgUg/ONjaYzCDlEHlc2JEMSEWDlKtWg3dH/dKAnXmhCLkJqESzFmYFexr1Xe3knPT0/YPGAGHSd0eY2Q3+tTrDkFjtqTYodAu43zG/sdNgnrakQgDosv9oiZgbmrnUpvHdDiXOB/s4RU8w+mjZl1+Nld2fE5li2ORpQIYIJkoM7esFMYH34+GOAcD/uHwVSG+RZjUPfpBEIpBUD7EBqHXjsGAjIbOCQPwFxR44OKyZB/Dg3GrPFUMTXcdCa+KAdpRTMlzyWB3lYjHpwTSmA4gM2MxqzVFhUmKNlkqSaAN6TddB+jgYCFTEhk0gGljnti5ne1/p/OE+3Z08kqoXHSCUrMAfz19nIikADNQkCGE2IEWahRaKal7GYflTsAtNFtu9BWQ6mnxl/rGWL7uN8hwHiU3gfiAuYz1sLb372f3nrzzbS7i6TYMq1DDxAMKPgrTkxmGQzuM1QEhN2Pca/9/JbReMEPSMBxv7pSKd6LyoOLi0n68MMPBey4GRkYO7APS43zOhj2CCXxPOgC/Mmnn6SPPn3E+RoNxWjDuQXAxPYBIA0AnP/yi3ckn4FmUL1++tE//MfSYNtVSSwSE0wOrdVluL+epQNoyEHX+egoDfrPNF9LdfdFcxja8CpQREm2pAm0nlHq7bOcMiFx/luDi11ayQhsKhV4bq6ViKONRROLse4TDDtc1/aHfgr0IOfSshv1xunps2dpMpvKJ5tPCSTfvHtIm3hxqtLZWwd30gCgVFRwYLnQ5ncEOK/cnToSIgAs2fRmqUQ54jVWFIRvcHFxqu64FxfBElO5NyRksGbms5NIHMXER8k0XFiV2coO61wEfVD7Fecl7NV4fRp/RzXJPBM83KUYdoeJp56eYzFTIn61VPfYwSIqH8Lvn6I9Buy7/RWXtoamohmELlFeL4IgwMZly5QWM64faKpiPU7J7ERTKs0PziWVisc+HQ2lFz0YcT3O19KAQzUPmHGYfwKasNP9QZou1tRehnQDkv47128proimK+jKC/8eY8NkefhB9tFZZh/l/C655NiyVBwMxmBdkXnfSV88eUr7hHMYiQWXFHe4D3XOEogMjUmMKStRzl1pE2dKzCc0i3VfAiInJ9P05PGTlHpgES7ThCxb2bx79+6m4TASuMsFtRBPjsXsXS8gfTSIedX5wfuPihPjA147NkH5fMgSIKreeZnXNmBwHhVb+fq50i5IAbaTlq7IlR1ad8vljvzA/pjr1wmcb337bQK/vS6kHxapu1ZlT2NX7OvJDteFlb4f+4ezJRJRvTQLO3N09AoBxp0drD+ccaoswnnI88Yac9XQZD+/wkU2cB4/Z/jpin82x9mxG+e0+HszT3qSDMRlqaT2jF2WpMK76mv5k+X3laSh9u9te9oPXD9Lbe+zPxDArYk6OcZyRVylbVjjQsBdaMeJnelf6qDHQ2QA0ABNZmsFonwVADiNoNeLu2z6wYG/ROsvD/9lxdJbdlRNtdX160WhhW1A7WUBwMu66V4FAJrqvtFM4wUAoIEB3n8wH3LFTFFqwucwAh3jgeEygPkyRscbq1kYmxqKrcX4JQHAOgP3VQHAegNvbLyiu/NV4N+2cTFAkg2tmYRxwOVS5LoLVDEe5QbK17kEAOyuIuAMYHDdPZdI6mgvPX70KP3i3S/S3/zN36b3PlylKZwqt2efndGA3tgbMvO+MwSAAMfgIg4oOYoABgjigJNdGKltACD+3oQ9cpbdHe3wxmF65ZVX0s2bN3joAwBkOSw7+2GtuMlDc8C706XWYfsgEXOk6PYZAJg14gwAAtrmfo7xdqluDlyj5MLadAxuo1OZx15dj0NbJBxna2TY7jx/DoaNAED8awAQBheO1HhHpWhmES5DwwRIoJoJQKAdjQfQGGOYBmNpvOCF8UeGW2t3yQMRz4TAGGOH9yFzzu8PAX+Mlw4biesyyRnlxA5kmDmfhmg7EI2WRknDmJP9awBAB5I1AMj5sOB5lJrheckepei7GHcuM+T3BdPOTJ986EXm3YDm7lji9PhOlqhX3bTgZFmfiXNs5lWU3WaHIAIb3EcJ6KnsUhq19b7PZ06UH3Odx3+jNEil1I1ERelkEKSAhlcwuFjmEs049PxN+QDHLzfH0kjk7zZjO0p2DcA161gdJr0nyFDs9tJkMkvPnj1N0+dndMDhGGO9mKlkRzkDChUzz4yzDABGgENQKLpEGgCsnY2Ws1Xps9a2swEmAvS/qulUdYEaAKztPIFwloqEhQoA0N0sp5M5HXUJDCSWAiPAOhwO0tdf+3r61ut3OedgHsMRxXbhfo4MvwFAMZcjAOQ6lN1CFz0GbBUACI0i2LnnZ+e0A7gHjKt0rwCUh75mJCBK5n4JhGvvAwAMMf01OryKTYO/PXv+nMzb6VyJHATgFEU3mBYaohnYjQSCteKcjLAu32ggOzUcak9mSDdK9SGKz+Auus/anxCDBDZZgBoAQOmEmSktwATAh5qCjLjv+yMEdoM0YHOWQp+z8FMM6HFfBYO88QuL7op5LQIUa5pqkFlXnMMsMTJDKj/gdgDQpWJsnlBUCHiOgNfmvV0wqDurhvlbnmdNJYSBRYC5EunXvoqAymXeAXiAici/rxBgq8spXsOBgGckpBqdT5Q4AnDuptOZJCGezcVwuVirEdVZaLmdrsVGZ+dqlAgDBACbPjRdqQ9IRrASa9ZeAxOQwO869PNMCIl1wSYGZCQvOM87A5QmDggAwk5Jm7ST7nz9WmLXyds3dQ7OAZDD5olRlkKjVDZVpZYlAHhpYjj2exlY1baJdjgSdtsAQIzbBx88uBQApD1AcydqR3UJcAIA/PTTT9LHnz2+FADE97IKY7lKXzxRKfZsLkDh/utviQkHFt5wmA6vH0SJ94JAkgHA63u76cbhYep1n9BfGI0EmIARBh8M44T5cbLL3YB9fqrUO3SqsK5hQ1FaH6Gd1/1qpi6/OYEYCUvOFZtVgKkF4CgSL1HJSQ3C5SqNBlHSu+wSaJku5mRen03O2GX5G995g8//6PPj9OiLL9L9V95Mt27dSoPuQAnRSDaj2YLOIVcyhPZkzB/WiRK3jSQFz3P6udJJJoiM9QrAP4C+bgqmdUh5+O/0I1ardHp6ruYPZ2ccV3SvZUlq2LbR8jl/j4QOxns2V1dZnCWYz8lZlIiuxdjF/sWr19W+GC5BohukwQiagIM070K3bpLOphMmlkZDATROuvT938FwR1NblgBPxejl/okmF1g/6CaMtT2Ffh8lgKTRvIdOv1iD0I3G2Ax2uB4WqceuvljTjx4/IkONNjxXyIgxjPMOvtV3f/BjMYMHSm4BAIQdx7UwBru7Onlhu7IPXSQQ3bQLwA8ZjJZzCeDzfDLjeKD02kAhbVIA3gYAy5JJAIAoTed9mGAD4gDGrqfExQrNPTC/x2cEnru9MfcdmhQCEMf5CSD04EAdqcHQ/uKLL9Lzx8f8O84TnJMoTddZ0CRIuDc2iDttIMgJWTfv0X4KWY9LG182fqNtWQ0ANgw4J/B0zawFmJEbJ/33VArfG7UAwLe/9Y1gfKO64KsDgPaTZtHdugQADw72eUZR63gLAEggNquR64l/2wBgfSZkoC+Aw8uAtm3nypcBAevrXgYAlvdX+oX1fb8sAOjP1esz+yU1sBo/lwAgr2Ff64//5N9swKrZOQotibAA8d3thQmDp+u1a5Bz7fQVAGDZ5W7bAb85gG1Hz6XJJTLvgE2T1GY1cEkWmi+bnAd/4yWadi9ZcgttIa34QhNp2/3k7sp6OwwTgQrPijPRDuA9obmSaYulKgZtGwCoL6qYgBs7KYDUzPBwCWl7uXRbRZmbM0jQ7AXinCXl90UIfmagBDhx1Vop/34Z+McDMxwfvz8TRPJj1uunXWqJj/OA6SrDuQ6HF23sOcydGTNci3QiEePBtfThhw/Sf/7Zb9I777yTPnoojYdRN0Cz2RkN9+GBtICGCd0t5XjwAO2KSTBfQhNwmRazYJL60PE6iXWPgxUOCrreyUhFYIPyicEgvfHa19LRjRtiG1AcWg0oYNSpD9aVyDGzSwauqGkVTC6ysJq1hBK+sqSy2X/t0nYw9WQ3FJiVQZbXAYJkBMt4bpedlUC9GAZirUF0uyzXdACD4BrXo5MVzCoBUuriCzF2XJ9lWOh+acYStAoDbMDf4YwgozYeD3ngIiMLxwzOI4A/3z/eC2o8fmZpYt2sxCVntos9g2bB3EQAB82WcNhKsIZ2gZoszR5sK16KCeb5MOvLXVEx3m6wgnWBIBGOmQWp8+ES64QlawEk5P0UYth0yslc09zAGTYLBoAAs7so9aEIuASxDQ7CacALvzOrz8EMfeysEReCz1z3zTrRPmjsXll6Z80cOKIcf3ShDrDP2Ws8FztlsmxHYK0Ddzjoto8Ckpq1Xa5zlTOKPcN9Xjj5HGcH/gEu236ZqQW9T2oBPrtIF5MLBjrsgOxmW9BwoiPrMpR2aS4YgDxe3IgmEnYYJ6w5QApih2mswNgp7b7BHTO03C0xZ/erZl+1hl0Jvmoft88hr1uIpF/2EvDuFexxbp9LKHWiflfYgXGwm966fz997x98L+0kaUnOFwrUWPocJdkIGHtdyAw0It52nAw8rFcjBjo5AMkOhdgN8AAAIABJREFUExzbZbpgIySx/l70HGb0+jq9kHKw4w5AmjqiAIJgO8jgWqbjk+csKUOnYbxyKUl8Hb+bPlY7sUmwjs0f9HsBgp3csZd7Elpk0V3WpeNgjjBDH8w8MAB1TgUAFSAeROzZ5MKOIoCbAAg5zmjigfWK4JdMbbAAh2QaS8NMiQ4A8AKCGsaqgJfwG+M6ZjLCbvteuMcr/8mBuxmwCJD9EtBV7FecK8GEyl0ew+/JzniVQPa55G6rGWTJJfg6K9iVk+dyNH0Je5grQcwUDPuBLtUMiKJEUKWe3TQKJgs1GMHC7CshNVvBbi3To1MBA88XwYQZ7Yu5utb6nIS/AdAPIAcCYAb8bCCAsvnQIot9RvtAhoa+j1SlQjwcpYBkVkeQP59P6BvsQAcNJeD4PxoOBKOzP0IFwZ30O99+Q01N5lNedzabRKI6WKWZMRPrLSQJrmLPXAUAOvC0ufJ0ggGIcXv//Q949iMRwOd0d1Myt7BaGgCQXWBXic1DPvl0EwAsbRxAGMz/yUTMXjAACYyO9qi5BiARCUs1D+qnfkdMdDDIuN/QDKbbS+O9Gf2H6/sHaW9/n92W8b4RGFkA+YL5g21OW97X7/k3rJOiTJH7JgJuaJBiHVCvuIg7St+BZ1Ynuux2Gm1MJi2iqch0sk77e3tp2B+l4ydP0vlU6+FscpGePHmS7r72NZWkf/40ffLJw/S1u2+mO3fuUNPS3a/1vKFNGgCg9yy1mck4RbOnRvvXNgn+qP5bUgd46Rn60sJeQnKmqSxAkyKsP2vC0bejvRXwjgoynjsL+CSLtAvdQ3b81fienj3nc3z28HMy9FfzpgIC46KSWDQZkUYg2m6Q1Q+GG5ouoIEPgSgxJHss54V2b4920s3XcK7Trs9wfwt2iWYJ7iy0FVnE0knDvZFKj4NZaCB0SLs7oIYffFB0Z51cXKQJKh5Q6bOQJi6AAfoq0ZjKgBrODazXw9fvpfv37qVbr7zCvXF+rmaAPs6HAQbCZNufoH3HfMAOXQQgau3JmCMTb06nSsRCk1fns6RzcB4ySR7jwnuMyjyMH5qA4DWbKyEMnIDMv/CHZvNzXhclwAAYAQDiZwOAOIdAbrh1dE1rJs3T48dP0rNHx/I7oWnbH+TxdmWh/cy8PpEp2fKy/2UAsAaeOIYvAAJ9yWXXqU39pkkw6fnBFOW1bb+D9W/ffr0GE7+fOn0BoGnYJ9MSACDmdzyW9jwYgBr/iBcDUM0Jq0vcNJeEGwCcxHnOEuBDlQBbrqhkACLBVYNk3tPlcOYmsRWTrS79rZu0GDfI53gFg2z77nJ8m9JdV8PZD7rcz9u2Dq4+n/Spy+7H16wBwOb99mfa9/WyFaD2R7NXVOFInX/5r/51a+qNgnpgOZFFV8kmwynEHABgeTCWJcB0xH8LAGAJ2G0w7YouS3LWrHFm6ne7Dzva1JevjYWVZ+QSALAqNakBxnyv4ZgaCG2+swKUCgCQ7BU7xl8BACxL9fx9f18A8LL7bhbuFUBivBELdgONLxhgl27YDEDGfJRZ+i3lZzXYt9V4F9ew5pTXsDdisy42AcDWNaO0atWRgd0GAOL3q45EbcE4effd99Jf//QBAcCnz3SQDzpinCAgUNffEFlPc4FVdDjAzlDAO4uSsuXcJb+6K2swmTI8X6n0ZGGx3o7EqxGs3Ti6ke7efjUd7O/zO3CQgngCwBZOLBxXHIgoOaJ+B8sX5VDKsYDjZmBY8wNAlSW/wQLEfqTjasAva8bpc3Bs8xoNAX2BaXK8VD6g8gWBGWBKAUiVQ3ExsUhuM0+6L5V8IIDG8wJwEUNI2j8unyntiZlwymQ6uy52hYNrlrrBwXKJJViE4zEPQWR88bp16ybfz9JldtAFaOdVU94nykssohwlN2Rz2FlWFr71ylpi1kxr/7ku+cR9AETFy4ALxzvAKzjMpVYU11A0YcF/70QGONu1qhR/OVfps8AB6VUZrIVDCQDF82knzwAg1wiEsOPzmpcoOQQYC62a0PTLpblRSlM+dQkAEkhD8Mo1jOeGuPWC8++Oyi5zJjC89HkQovCNAdB+Ksrdy+/0Oel1ZACQIARBXzlwLPsrmUEo9YXjuYJezXlanEzT2flZms0DwFqrRI0iymR/OfNcOSZxTuQmEAYcfJ5EwI0AEC8APyr7bI572sroHmpAxWC89m+U6YUD6udQ4CiPyyXc5e88r3p+jdpVjlLTJbf5TlyH3RABuExmWltRgvYHP/hB+t73vpfOHn8iIHV+rtLSYs44z5DFZwl8lL2Fo+huu6vVSM06MvAXAWKsGwBhEu/fPOfY7TV3Ag+AJq5jABDdC7l3mMwQAMjxAGNrvkjPz0/JYDi7AMNbgDn3anTVzfqgFAuHsFswFWKvQYMWQAL3XsHMwFjh5/FwoKYTYOQQ+FEg6r1oANh7wkxAlwCbFWxgoQl0JAOAQBLnhAHA/ggARj+X+IJRjXnMDHCL4YdYvyUXdEZAuqJtz1hqHKX/Gjedk7kJjiVUrA+aGVAWzw6/wUkmN+OIeUMFSel7ZGYH348Osy7xj30Tvwf4Q3bWUhpeTQLLzMBgT87Vhd5qbj0Hci7RXlzo8wFAUFJgtU6ThaQ6ns8ApCzSOddxN63HB2KWU0JilS7WSnDgnIQdAeOmkbsAAB7jGfbITBcykQDSBgBoLVE1WcB+EYALaQMCDaFhNuyji+2QJYVYc7PFSTo6Okq/8+03KSWCVKfAIzWXcK7K/oi0apsuke3Zfjlb0f6MmGGXAYDQWsb9mAmMtcM44RIAECWUKAGuAUACefS/tA5gw3FuAgDE+b9YqZQepXLwt87PVfqtBGyfTVR4RgYTE+OPBEB/eM77ASDBfRS+KbQWsaZ2ouoBQCCBuRCt937p7qgJC+4BfqOZYf2+ABWCcAEAsuQ+bITtA5plsXR/GEAtgUOdhbjfQV8MwMVUTcLgN+D5np2epM8++yy99tabBCAef3acPvroo3Tn1tfJvMLz4PPTmbqs4rn1attRQHcGAPX3aIZhhlwAgGStWY+T4yUtbQCqZL9mWZtYt4CaYlGo1FM+n88Z25FR+EZIdDDR1FlRg+7dd95LP/vpT9N80m4SCeCWXb/jedBcAdfFOYznUGk25knrhDEdm3c5Ua1u0VKp6SR0S6I/jQQpWJzhpy/iuadLnHu9tIp1wQYlhU/So/5mJ02XAhIhAcBzMyoBcF6wgoX6tfC3ImEE7b/dnbTaG8nfwBlhKQf6BDoH8TmCzS6NDb1aVYCs0rg/pn0TY7Vhb0LDEvc5Cckh2HG8WOkwGLC5KF5YJ7CxsPGMN2B3sDfAeMZ8d0Zx37F6wr/BugLwCY1DdjrvCvAHAMhzAlI8O+N0+9Wj8HvUrAVNUfA+PBdew7C7Xpd1N1+XUNrm5ESgtSTzut60ZG3cYpulS2nV17j4lYEgJ6pyRVpRwxVnHX37JZLv/dQZjlnajZpmrONvvv2WpAiG4XstJ0zmYeXSHkQX4ZcBALn/AX3jXCLxYZUOD49YOYamVgYAuY5C4sfSQHViuB4T7xPOWdFstC7srLX/NjSBS0LAC0qyy67C+M5lnr+rgb/S598+m5f/1j5GDRTn8ShIHW0s5MsDgOUYuyLPV6m7BXf+5F/+SXg2MQBFRhiPI1CtKW3I3aci6+9uIn70WgNwo0vqxhi9zMCXXmF1gFRMOPxYMors0OUNVjEVLwUA4wObm7gOBOr7h0OiGuuSIVIDhf7ZAFS9MLPmjYmEBTOBB4ufO3ZGU3obmfWakUHtiwLrtYHJyMQl83CpRqM1AZvxeJHBKzWFWkvg0oXfdNBqrb8XAIAl+Pcy2QeOY9HFmuu96vaXGQFx03ja8to+UFfBAMxAlxmA1nQaQoemlz765Gl67/330s9/8Xn65S9/mTppVyBHlMzs7wwoAj7oqotkP7puOQM8Wy6Y6XNguQrNqXqDG3jHbTMzCcch5h8G/Pr1G+nrX/96un6gZhdoTICDESULeOFwZeDV67N7FsAsvPpg2hVry3OOMVEgE4FPfBeNOkGRcA6atsS8HjKB5TVsc9yFFgw0ldRGd1yLJjvzPehLe8jAP5lYRZfUaNwxOb9gV7SySzAOLACjvL9sN7zRmqDOJc9gAaJUBM4/GTZw7PuiwCNIg0NFViGYMGB1MhDAXDXMQt1ns2cg+1jaJiQM7FxxfRoANPDmcY59w+uFoLTHTgMrcI1C7sGeosNQMLt4AC7ExKu7NxK4Wq35TJqfuGeL7bIrHxxWlM2ovJFj4i6S8X46Z2aiZbAkWG1gBxvQyCXHYuRRd6wFApoJpy5jHJvonCw2p0YR/y0GnLrrQezZZeRlYAwGEzWd1tJwA2NCLycaxKRzproMPujoBpDRsmVxXpbrOZcg+m/dNdcPXiwdO5sQCJxMUGoK8FxdCRWwFKWxG3bY9lql5B6P7GDEuYAMO8eDXbCle9TMBwR4gsEYJTs+acs12nZedL75GVdQnzYY7rO1kADw89ciyXncgkEMyQHuw/A/qHrAJgkItMG0UuntcDFL9+/fT//4h79PoH12/Cg9fvIkzaYXyoTnbmw+B80AVEMfn5s4d2mzFgAYl7nkx4AqAAuDb1hHsLsqVY5VEjpD5fzTV6oc0TWE7dklVYwTNotggkGNi55NzihOzsZLhUZmJ7rsIoHhZjolKIt5hY284N6OxiDsUqkZdKIDDA58Hv8SIGUSB466Ai1pPbkTdRdEvaw5SMaHmaXu5mutMbCdBv0EUWk2AYmf3Wnd0gEO6HDhEggk+EQgJkawGjczeMgyZFOD5vzA+GUNrX6z/rwu7bMq4HUJVbu5V2N0L09gwqdS51d06dW5D7skICGuu3LTHvudAipwPhNIidJyBNTcJ2uAwGiupdd6eqa9hH2Kxh9pQKD4bCb2+6SnAHHei+7UkQhzSfsJmjOEncS/DIIvBCqqc6m+NzfvqxiQSAB5/3K8fGMxP9aghf/BZjGxAQwMrJcCwL52/1b6zne+k46u7TAR1u0C4J7nks5aC9zMpHL/XJUkqG2t7bUAwDYQOArt0HfffZf7bGcYpYzAFMj4B/CEJjhRksry9iG16FAC/NHDL3huozkbXjg3XWGAn53EuSDLvEsAUEDWmE21np6ciXnTl5+Ac4Rd3BEwQ3tvIGaOGJm9NApmU5dNKzppQAZgN60mYmSlzOBpl3KvBmHPY74MtK7W2m9u/gaGdwtIj/382r27akKFZh5MoOj6ACLhH406A4HLZ0o8rwbQOB6n7mggfyoAh9l0RYDl9q3baXcXLMhgstlXDGmHJpCPxLETyC7h9b5wrNKBbwX2rJOYet5hb8T1OJ+F5mUFEBKCC7Aez4H/RlIbwBrsJTXKVut0Y6h9tVxMeD64adxHH/yGTfqePRWAuYLmIDU8BTCheRvPqmD28qQupBP0HqyvNrXK8bN9aGgA8nlwTwDgYv8hkc7EddYAUzLR8bQZ3acXp2R80R4wwaeOurjMcDDkOUCbhWoCAJfhewxHe2l/fz/t379F/w1zCfsD2SHao/WS16XtIIBpfVewW6OMGgmS8JfIqKOugEBm/AX25PlEGoZgSNLOJOj6DnhfvC6B06YqjyXBWMvgVlIKQ/sWmo88ry3ZxGZgy3R+ciF/OanpzCLsPcZPOoCvZsYmAOxHn31OPckBpV+W1Da1HeGzYt9Fw5cG+2hg68zqc+Vjoadu+/SiUtLahq0L2YzybyXgRb+IjeKbcwfnIRNBK/g1PUpGIEZbg9k5GKRvgQHYH6TRQPamQwAQFSYNllLG61dpAKJbMTUWUXW2WKb9/YN07+691O1rLSgBLu1hntmYafjlVQlwjREYoMrPHh3Ay591fnma2vvJZ0YG53L4Fs05HXPEx+vnhP3f/roal7rkg1t/zbN1i0bhRuy7oXXYBgA3mX/tv28bX9xQrnhxAjwSFJ0//WMzAMMgZ0S8XfOea9CtcRILCd3eXsQArA/+zdH5sgPdBpxsEP3gm2KRFTPI68iZ9iu6sHwVAFBWs0plF6XIBGKs7deNgNMwbP43KKkVAGgGY14I2SB6Y1wOANKQ5oM1xjGLJ2/OgzMFNNyF4dA1cijUmtLN8bJ5vYxj3Djo23aOA8KrSoBr5l8TtCqbWT+DxwIajJquBvixwSHAUTFGawAQpQhwPgEA6hXjXwCACLKgJY17+unfvZd+9atfpvcfnLDb3LB/TZn4aCJxuL+TdsbjtF5c0OEadkMrJQLbCbRACIIpc7oOB74EADEHuRQtMl1weM1iwZjeuvVqev2119JoqEOFzh0d03EWYabD25GmiYAsBLNi1LTWUjx5eUCXc6mxjUM+d1XyeAuAK6+H8YDzAocV4v4GAOHYlC/8vtGq0l9yiV+UklqYfnYx4cEPO6bASAxBHwBZuyQu4MRAE+jFQRoADgW9+z0yHg2sspsZA349KxxlfN7i2xgH4HkGV+iYQoOktBVraTTm9Wn7VAGA/Dv/1m2xD/G9LJUIxgvXQpQ94b8RyGTwZoWyEQnGM6iODralAyPQCM8j5iT2oQJOBbhwpLA+wFahlgx0a6JLG+8lxqLUF2zYl2rsoX0WOjm9BmDG709PTgT0EcAS6NFyXKgH05TKOjuOABXrFQyH+vAlAAMIhCAbdJ+g5TjJXaFxfTwqgfno4usScoy7GahiU2i/W9OtXJ9aO6GdmQMhOcD4DoA7s1NpDk0mYAHO03J+oQDQ2roWgTb+auaSzellCZqc4PEdKUCmzljBFEMXXC02PQt3agC222xmA5BqzwIA9HzU+5gAJsTTwex1F+/a4YLOFQD8YMr53lA6jDXVt6QCivUWi3Q0HKYf/ehH6fe++23+DADw0ePHaT6bcO1pvLHGpRXVSQLjWWIWzGDOVwUA+rympij2TAEA0j4ieFwsad0NDForKYOuxfW5B1Byls9OabmuwUgOrSSs1aeT8/SUHaHRVZN3pcCyaHjT8q/MQCIzo5umoXXIuSqaSVibl/12IWIPwLvYiw4EuL9jHbPELkpEKVfBEkQtj5IZp/UJAHCQ1tH91wAg1pC/h+8bjwQwQhYA9qcI9BnY5FL2to+BJlZcN274kwMJ2VY1Qemm3rjxsyRZ0YDTBgBLX6B1gESpfv27bHsLPVMwwrHelFRwuSQCqrDVZgYC2J0jSMZ5ukzD2OsG4ADw85ncnXcNBnWPzVTwXJMVymdn6WKO6yzSWRKDcxFMkYtgSC1DKuZ0BhuHMnWdjQYB8d9MREXE4+YDaoJQgKYGdt0ABn5GoZE5HAVThsy/EQFAnicgD7OUb8ox39vpk5H79lv3+T4wQ2C70WyAdiLiilzadYk7uG0uXvw7MZzI7CuYgCUACAbgOLqlklRUAYC6v27IoHRZAvrwk0e0z+gijhckVNR8TfYS/gnm9+T8PJ2doQtpj787unWP13n6HN1l++wmzoQPAQnsKbE1d3cHtCcZYI2ADHg2k/kLacjt9myfg0kfGnRmaK2GloLQKCGBgXsEIIl5RAUIxselsTzbqPMZoxoAxrojCQpUjNMPCGC0v8Y5KSY1fn++Qol3P12/dZNdjHF3ApAWrBR55eYrTGATk4BuqhO13ShpLPaxjp2II8ruvK4aI5NepfNYt6oyCMZ+MFeNv5TMYbGb9YCubmByLZo80E5E6f5wrvsCAKgu2aq8eXb8PP30pz9NP/vpzyW1EFp0yJiSWZjPVwD6AKb0HFgn9t84zjkhFXY0fDv7jCgixmuAElcw3HOALimdOUspwTAUgOAKnywV1Ze/OEHtOux0AGbYZ2J8K5FmJiAS+rDV+wc30s1bt9Jyr5cOD29wrWJdqloF8Sl0b5Fcb+JTfj+bkjQsdBAX8MoJmZjPZTANF11cd0b7xlf435QgADA+bXSj2cSIMQbmGcw/aNGKrQ6iBb4f/jbPcXdZTqpomi/DPjusBbdruSAAyHFJC5IcHn/+iOt1bwyQeklJBj1X3EduMhiJNCeb65Le3xIACC3Pba8aAMSGtXY7kzs8t7HJ1KQQCD41JMHIRwnw29YAVNxXAoC1v4bvf1kAcAqbMkdn+D1qv/YAMEZDMwOAPp/p/1V2vsYHvgwAyP1bXa9OGjV2QAshV2jEIP/3BAC5/EufYksFY1MCHmd0SB8ZV8gMvjjDnVgsCWblGGcmcjy/KzFzpcm/+Vf/G78pO0kWGy+AQP29LQK+KcIc+zunNoOlYabapQf+VwcAabijhLDUbmstsug66hKLerNt695bvqdesJtMPr97k5lY019l9NsHngOwTQZgHBgGTGIcc7e8Qny+3XijGU9+v7s4VcBf1q4y4yUDfAEE1zTaK7o1bzNiZUB4Wc06Mp54+UCsr0Mwgxa87bj6fbnJS/HBEmn3r+t5zOu911ZRqzUAre1Uz3LzHdsBwEakGPozw7TuCYj46c/eT3/7N3+b3n3wjNpPQzRfwIGHTnsQjd4f0XFaTk+kWRelsPOVujlljbroRlwz/zK4E+sFhwSMJJwAsrT6fWb+QN8+unmUev2LKJNV90wAmgQDo4sbNLRwMEOEWJnYBkAqjVkeTzMOKgZqve5dEobzlQdwzpDAoRdTCY6n6eVw8Hlgh7Yc5gOlSM7u5nJFAzIh8oz5Q6YM4toGADFGvnfch0vrcikXslBe7yExgGAdByzWIbr8orQSjsnF7CIz/+A8gYZPLSdmRMWMUXmDnmcxQ6dGEOolqL+KzPjm/vE+FDhnG72xT9Zdib/PxERROW6jF5cPPOvnFKw5aqhcGDApwKHI5gEAwPMqaAj7HiLaBgABtNjhZfe6okzKJcECZJonrLN2PKjJ9MG9h1ZM7HtmdQGMoRQNzQbgNPrwY6ZWAau7TLJsFsBjrAOIgHuuOe8hDu676fbAWJHwupg9YqJlADC6/zGoQhCBkhoCpdBasW5RU7LeOkuLrJ8DEpw3LMGEY479PF1Jwy4YuLOpGAcghOB+ghiSS9yaMgQ9AZgJOmfa508pWafxbhx5arVEiY8dMIGkAMzUfMFAXJnB1RhX33NJAtW/BlOiZK7V69yAUGYoxRvAbOVaNzgNx7/bSd+6fz/9wU9+ku6/ckjdvMnjZ+nJ8ZO0Ws0YwFn7qtNR9/NuaOwgg825cYCVS5xDEzX8HgOAGlvAlmLKwfZRnB9MEpboI3gfEkgW6BR7NKQuSg1IO8L4l9qT0I4Lf+psOpU20clzfscipB6gYcRSqAKs0dDILrgLMJhjmrtIZFUZdwRmWKdikAvALwHgrruHByic1z33ltiC3E94zgCIeRyzazmAPZR0aS9gXVvzCvuY+7834F5BqTHPolyKJuYyAErYX88LPsd96gRtTsy19TatedgbF/5O2DgBAO4eWauk2te1fY2SsWKcS3sLBnE++0MTtrQ3AADNNGKAGh2eAQCyhNgAW9H8wAAa14UT6CGtMV13GKCer9Rldt4NfVUmi9ZpBu0/MP3mc55HJ1OdS0vLPJiRw7I+SXiUgL79qLw/K0ZIZpAF2wSMG5xfO+gkymYBYpJyHQXTGiBLL03SbWoBvkaGLhQ6eF9ZI1rjbXuyUcK1eQC+1G9QcaCuvrD7CAb0sRH0/Gaz9N57D2hnAQB6/zEoJBAIvluwp18SAPTawPUALD78/FF68uSYlRI477/9nd9Vl9yLOf0ErnuySAX0jkbwDTrp61+/m3b39tLOGvqv4dtEWT99DSQsAS6eqwv5ciktyNlc/pq18abnwWwMxhWASKzHZW5yhLNVmnhk+SdIdOCMld0bLNFdF2tYXW37XTUN6S8n/PtOF3IVYJ6hYRmaTvRow3oBYCJBbK1dVkHsSXPNgEp/UMYjkOYQwGYg7/AQCQKcp1rnyPRRKy/YyWCS0t4ACoK0AM9wgCE6HwEUlYkdd0nGrCqxqrgB6w4ArtcfNACxv8Y9+QvL+UQ+XQB0s9kqgT36f/0ffx4NPAB+uzu8KiaUEIomfT63CKA7eAco1SRz48RuA1jRBdY4r4FX7BvNY7M+y3hKcjWgZ4NtjKZWIfHQE9CW1zpKcwn6BeM6Evij/WvpxuGNdPT1QwI5qPSgVl+CnMOoSVCgMRnOLDRjYYJSUjo+RxRf4Fyb024haYG/zxeKO87DJx70x9wHORERfhAAQNh/fmdK7DJN+5iwzgC670lSBVxD3oebyMgvRjyD13Qm/wXnNyUIkOJbLdOrt4/kFycluo8fP+Z6v7a/r+7HueIrEnRuZJeZ4x5/2QvgDVzfPocrDfmXMlrFm2rJiw3GXCR6cpVPpZc+GB/KXx3uSGIkMn/QANzZ2U2DvrSuuyt1AYbfoAWi9dAwxLffuTS8V2kdXZZncMkWaJAzJoGkNxLQKsA59Gbje7zvyivbfuZzxgSoiohV4yEmZDQKPc28bLtznFsq1fcrAEETqgLorStWm2tdQoiqekiYkXrVvDd4QfNOx6H4zQZQarfDFVeFQ09/Oi5TM4qzVnSc6zUA6ErMDQCwGaZ48IrBdhkAiAwFDbRZPA4U4xuu0gC8HFC7bEjbEYe/38y/3GUsWAzws+1QboJ5BbBZfd229+otlwGWmwAg3104WNsAQBgqjp/LqComYO4CbMKUV7QzYRDlb22eNgBoLSo71DXg6QXUPG8bAMz6Wl8BAGxt/EywazPuDADSqFago0rXxAQwkFluJB7cW5DFerPVGY8yYFoFw873So2aEFPWBm1MCIGqap2s1zJ6yxTdwmgglLHSalHm92J+QsfvnV9/kv7qr/4q/eLXOoh6SzGCYLfd/IPXm56qXC8YI9O5HEBckcwT6NDh4DU4VAH2uctP0fAA190djanRgk5tYKh1+me67jo0W6bLdHaKzqRiTu7tXlPmO0pwEQTzuaJEcTOjo8CgdFbKfbMKbT11wQ2GUIseLcDJZZ4uu3HpFXVaKELfpYMNp8MvMY3aL3TTpcML/YrJJAMEBjxcamZx+ey8RYC/xHuHAAAgAElEQVQwX0zpmMLxwAEL8eb9/T1+N+4TACA175i17KSnz57xfQRzDw95M/hvjDVeF+dTApFwRanNGKU9+JvXrcauDQBy7W3RtgCDhmVf0BsEg5NlygF2RAaOBz2A0/g8HG4EqhCJnk0L0f3CVgGEliaMHTxR/ZEBVgMQid/DX9Z7o5tkOGief4LfkTnexgKEU1eWD9ZNoXAdduVbTDcAQHwOGlUZ4AObDE5daNdgTNFF1uxXj2upq7hcCyjKGm8ByALQIdi8QumWghbc63wyVXkdAUCN87Z5sY1pbE8ARAkMVAXm+N75dE0AdzGf8t/pBEwwMGzg2C8YCBHAywwJrwut8+XS5TNNt2MdKOHwGHAJR5WldwEAcs2xSZN01TQGup5ZYQCQ2vZT120Ap+0Z7LLrfMveFh2bef/QCGXpag1yaz+hdIlMmS5kC66nn3z3u+lbb7+dugsxNi+ePGMJLZojYT/N5wJeAPxhfPu9MedHGjaNxh6Zw0iwoAkOAygBXQYAYavJjAVHrd9j1pt2EmVZANuTun4DaCIzJTw4MEBULq75cMDic4UgDsS61wo8z2fz9OTx43T8/JmaLXX0e2uwuXS6YbFrvHOgEE63baKB7wyyr1WuaEZgZq6YFR8AK5cMAYgG9OP3hJbgoDdUuXC41OqY3UmIN9n8gJ1MhwQAlQhQs5HMHIzmIGwGUjSTugoAtDWHH4Tvy2eLg+xh0/mb4xY+Q9k8JJ8PLfsZCepgVFXHRpOUCsbiNgeedi2azxj8w5pTkxDZpTxPsJfBZOJcZVxRGolginE9k6yzSrMABhZR+utmD4t1j2feKfT+zs7T+VL7F+cbSxhZmlgkgGJ72g8t/UHu68J3ZMlpnDtskIT/J+2rMbRvo8ybTMDITOygbBYgx1KJi2+8dTd997vfJcMNa8fdrb0O/lsDgO+//yEBCQCCtDdR+msAEAG9mkhIssMlwJ989ph7HsAFXmYA+qwhALhapfc/fMhu7r2Bmgn9+A/+CUuAYZcpvZLWCQ0b0ISA67OLRNI8vf7G/XRwcEAA0Np83oNm8pIZ33WFBDqHY63MpMnuLvWLg/ADxYRH8xGuxdjfILjhvDs5O09Pjo/T5EIAC0hhOG86s2j6sBb7q7sWEDgO6ZlB4OfT5Vz2tYvPzVMaB/DXx/xDt3pf59hKndKbOCMST9boXFvzRPtvZ4RzFkla/X4WzXRYIhqa17J3Oo+9fpAoIHDYG0vSIJptmGkK/1cVFku+D5qVrNogSxTNyyQt05mdcfyhJcjzfqHnxLw/ePBh+nf/+7+PpE3OqIfGrxje1Oo1S5llCkpcSTZGMg8+/8pEXEaq1/JnBxHPMeFPP1bjtgiAxM2+tEaaGA/+GG0TuzmjIYMY+dSp5nPKDnMMkSDpqwvwBFqBq1W6+dqN9Lvf/z7BHCXc5GcCcCHDO+sNhrQPwGfuIZwNOBdDI5HN4XDvAdAFMPvZ8TElTsCkxb7wOTSfTTmO6POL1y7+1uun04uzdHZ2nhZzNNibpVUaRuJICRHLLyABje8ahrbvLJoxzmBH8T7cz3qVbt66Hj0MZI8BAMI/v7Z/oPM87J+bR5XdjHFfGbBy4jA0Ui0GYgZkfX687M+lZIjPhTKmwolblv4K59C6gn84GN/gV0EDkM/dF7APDUCU4g/B0CsAwAZgjPPU58MlBC3YL11XTY+gcQn71e+P0huvv576YzUtNAAo3wuak7qPGtCsAcCswV9Jaxm6y4zxrIWokYWECl41gJrHPcNYri6TIfP4+X0vCwA2578u7Dm6CgDc5jdsWxt/XwCQz1bEjb7HKwHAP/1Xf7xuB5yGSJX5JvpeaABmQMvAU18GLINQLlXLuEkTkpffkzfXKqjBuSttzWSousnUQNTGCojPv0QHHjqLsbAY+hSfuYTYUK+v7DD5DxvgU6FXtdUouOlH/mMbqc6Z7Ap4bKigznBvByaz41cw6HSPZoS8mIH5ogVcGqocEF5C+fXfa4NnZN9BtLt01ePp9VVTfiGaXb825iAAPYEoOpjd1QoGbdurNlzkvmwpF1+4O6MNDllEKhGg7kgspGvXjtKvf/3r9He/fJDee+/99NHDz0TRhxg1Sl6Tmn8c7vTF1purpBIlHGRqhfYI9xoMMlxWrF+UocLZYNClrBtLJtGVC0Z4CsAGGRrdyP7hbnrj9TfSwc1rAfxJo67X3eWBenGm5gvW3rCOJQBHvARCwtj0VXaT9d3C0DpjmTWSmmwobUkwsFx6hudAAI4SXWVoBYyhFADAnboXwyGKjGdkdqGRj/3aCU1COl0u8wggy5lErQd0fhWIZ7ac750O80JaMplJF2L5ZQknroducujWd/36AUEgNLnAy101XS6NAxAlwShxuXbtgNk4OOgYP/wf1yJwQJKTWEGlhhUd2NxgpW0D6egFwxF2APNrfTuWloXEgDS+xJiDI8wSn0J/TgCgNCIJcFGPT4wF2wfqusjCF4GtnfrQrwnNQzcPcfmjdQXhiKPUCc9TdvXj+wHuQKtmKJDNAVbDdNPzWIPPAIDtCHZ/bpQQ2o+lXh8dcbK2mkBEjrLsAABQMjGjZLM3hKYZdOHc+CE+ZwFyM0aiVMYljAbcy2YSzbOIIWa7JAe9r8Cloz01vbigdhcARjLBZgjIlnTEVcpjEW9rXem+ELh6fnD9MudZMsBt40omJvc2S28NKobzzgAzSnyLJjQKYBtAkHYgpA62GtHCQbvsHMF4829RkpWB70gU5KZYq3m6fft2+mc/+X1ql05PHmldHJ+k4+OnwXBASb8AKcw3/k09lTaB6avy9E46v1AnTtsZGbRophRaiHbwOaZxhoPdi4QZbMTJ02OKjO+NQkMtGtm4pEfBehMcGlQF8EcmdTBDpotlevzkcXry/BkDJQTYZrnCkfZ5JwCx0TB0RQO0LJu5bSdeYGMQ+OLl7wdwY204PlskA2w7M0ATzBl8HuM4Goz4r5mHLsenbWaTOJUowk4MhgN2Nd3f20/9oTSl0CWVQAfsAJJS8JzwfNHdNj+EmYjeL0UX4vycRYlg6YOwJJhMw8KnCQZFDdI7UE1daZlm1nnYY4+NGcNkn5BZIhYnnxs2mBqA+D2SBQqC8XeNOUqEY9+HXS61+JiIGIwYzAGoQanceZS6rQBwQL81AlxUPMLOT6ZLJkTQJRM/IwGFVy8YQAKjm+eXdEPh42QpiQAmPL65OZdG2VdAcwU8BzsB7+zw+bF+B4Ne2t3bTTcOdjkWF2enHIfdvVF6+5vfTN/9zjcieTtRN9S+mlEB8Gdn2lVIOWS/v21BrmYIhs0iQz3YbQQ0NfYA6BCYvvsrNAHBGSM5Drhw7P4bfpkZhNjTAChms2V69OhR+vhTNQsY7FxTCaUJDkjYouv1sEuA8MHDYyYgMP8Y929+81tshrJm93mAGCotRXd3NSQ758+owLh7524a9894RltaQ2M9ZOKHybE+NHznaRallthfrsKAz7FcNJIZLV/MvkNcB+sYtpvSBuiAG8zWDhNIDbO7u5rpeVdgAK4SmlxAn/TJs+P09PhpOglNt3lHmnOr5ZrPAw1AeglIqMEehN01gx32C4BMPxLjJjagSUH5gn/Oveky+0x4CMZp2AefJ0hQscpirAAYTDS8YB9Zuuvmc5FAwDhwb0eSa7iYphuHh+yWTaAQXVRBX50tqGn47//Df0jHx8dpMBoL7KI9hC/cJGjbtsUMNQH6tTRRlsKJh8Y42G7wPEvRgAUk20h4aFydIGszms1s9xjWEli2BUgokYmNOUPToGDyPVsv0w9+7wfpx7//e+natWvp/OIpnxPAHOzTIHwDAjwhiUJ7Et3Lp/M4cyI+sHSDcy04Px4+/CSNRjuan7ROe3t7tCOcw2UwLzuqHoF9wF6czVCyO0mopFJJsLTJQcHDep1MppTNmM9PBVwPh/wcmmnZtiNhdff2K/ock/fd9PHDBzzHHD+N2UhjST88gzohceD7IWAeXagtwWUmVQ9dqLck5uuKwzru9WcMgNaSYXndFBq2tMuOryOem3avCdgNrW/4n9iPR0fX02uvvZb9/uX0guM/jISvmYCNnQ3cwbFTAXgywRNSLNOVEpg74136Yf2htEx9XsK/kB/arjTgnizGqYRtSm3ll2X+eb1vAoA1fqR31rhBuV/aFiiIElXlZV3B5s8g8exXvQ7s/7ev3/7JzF8/h69RV0Dka1frAU3M6vtvkc5MvIo3OZGQK0v+PgAgDRMp4sVNvCQA2DzQiwFAZ6ybUS6dGxz4NYATDIVgRF0WePh63oA1key3BQDWG39jMVwBAPrAcGY2LzZv0Cxevx3IszNspL3ZiH9/AHDbwvZ6zICeNUAqw1IfWCUAWF93G/CW31Nkuy/baAx+ovvRywKAJWMD1xVVfHNV5AM4HFE4+AYAOdYdlVzMpuv0wQfvp7/6m1+mBw8epONnZ2KOoUsgmHZ9OVK7IKvhAEIGbjhME5Z9zNI0iyAr4+9uZi7hg8YSmYcBANoRwAEOx3s6OecB/8Y3vk7q/zyYTou5RaalaTO9CLCl46670VW7I2ZCBpoKQLp0PLOmWDgtcJbo4GRAMA4Idv+SaCxeaNJB1tFamXMAgASEyIASgJWNLhleEdQOR60SLBtdOlNwDKARws/rOdSQI0DgCBy0J8T4gbONwApjqUBc3ZfpFEbTEs7TnpgBcAQk8o+DcU6dEQQGZk6NdsYcdwRPeOE9apoBQK6XljNlMqkXFmUafG53hQxQrLRjfLYKAFR2lAJCGQB0aRT+tTivS/UQ3MCxn1yIkdaUVruhgMALd3l1yRcDygCBVGonQEHfgfirEcqXo3bBsRuPh1EK1TQcEdNNgA0m1B0Ky2ctQZDM0gvmGtddoQu5LcHkQH1ZgffOVgMALUuY+2MxBMgZY/lLiI/niLGtz+hARl2nG+1NO4h2Kl0iSltSNNoah/bObCJgeHZxIaYq1ggSFQimgtHYWgNxPyUAaDtV2kEz05rfNeCtFrTF1WU7XvaVWQiL7QmUfE69IBHHvRpNHNDkQ68oyY1ADWLvWEMIRF97/fX0P//hP+d4DNYTBmiLJycETqk7Fgw+2JV5aDshAUOmc08lgRYJx1rCz73Y1xStor2OfZ4DPNkRrE025OAenqVnT5/kAJkMuDHKIwcMFLwOHMA5g8+fwewESBH79Ww2T59//nl6enai+e+AedGAeh7HbQAgR6sAAMvSXtm0JhHUsEBcEqYrG3S3PeH3BCOT+5yASYdajLSDZPIJvGZJFJIPALJW0c0WjJteN+2MdjgnN26+qvGPUmwAgGy+Y672Ul1v+SwI9IJxyKJDMjO1vmpAyAA15sU2vwYAZSfbK1pnSdMQBBIM3lfyE9p+FEkxOBdCAxJdoXldNg5asqkH/BM71j4LPf5o/kDd0ig1c4mhS68mZq7Mdb5OqV0J66M1AOYfz5YVklPzdD5RKTpKFJlAiVIna/Pa3jRroUl8aCCVmEQAjBf+yrEsNNi0C/UqAUCWgAKIBEAw6JEJf31vrMTOXFqmKMW/d/9++sH3v02ReJSWnpwCHASTcJiGIzUJQfMG7BcwmLa9vgwA6LHmswcACO1FrE0DgGhuJp8A4E8bAIQ9BuhhABCA/EcPH6kkcnwgf8wSGkwsQO9P/sIHHz9hqS4YnPj+r33ttXTv3r00GKlCAIkvJloWEyUXVwA8hkxm4H2d5ZMMwFHL1jq3ZIBjrckXwTyT+bNE+W8I3ANQ7whgd4Mfn6HeN9CMFhCl8wnAJMcrTP14gMSk1gMD91U0Y0qqrOjHOgFwg/k9g6YuEmq0p3NqgWHfnp5M0/n5Wer2dunrQPuR/kFoJC+mMzVpuFDpOroKk7E4lf8pH01+Hs/jsAlgdMvOtYFs2dQVm7vge7qDYEpW8SeYgmB4wn20PbD0AO57xMoC+Xn4O7Rk4S8f7OzSfv3FX/wFzxnMJxKmsO9q8iafj4BsCwBqA4Aa6zJIbzOIsv8Q9rpkzts2e3y0zq8GAMv95AoadnePbr88+xaQ9Fmkx4sJK4J+//u/S+bufHEmADgICKjQom9pQ5o152fBTFP87u7hkHiwlAu++/z0NH366adkjGE8h+MR/WF8P/YDmh3iNeirxBp2lOw2VCAhOQW7yK7vgRNkwFrVHg8/faDk1ggaeDNKacBvwh7H971660j7aK7mL5988qHmrSPJpV7ICuX4MBKerD4Lf4LPl7vGtzU3AQBuffncKuJe2+bSj4MEPYkRRQVXBoGwbqLk19+RGXHxi1nvQOOGioX5gtfBc18/PEhfu/+11O+riu5lAUDutcKPxbxinaC5DF4oAcaehD2FnYf75PhOiUXdmAHAbWNDn7z6Az7Gc7yogORZegnzzx9/GQDwRX7tBn5GSZKiR0N2b+PBKgIaAMAaYyp/9pxvXyQp1QCgz7ISAPT1yAivmoR8FQCwHP/OH/+pGIDOtGdnqMoINqUjcZCQYou7CWe1yCziIeoH36zUjAG9ggF4GQDYbKZ2AILSqS/zMhJed6m96iqZEVaU4JQTfyk1NW4uj0+hNdYy3CWmSkCgvQA3M0LbAcA8TkUTAX1PPOFVN3rFYJaHlC7bRqSNRvs+Wsh/BChckFnXq/15l6xsK+kBA6zpsnXJ84eB9WM4Q3gZA3CDimtW0hYAkPcdmmW55K5uC99Vx9iPPv4ivf/+++k//82vGPBBxJuZ4ZlE/3dHYr71QW2Hk4mkTr+XzqbQ3FgkBB4c69AYcel2buoQTAFNgSj8MMzz6Yzf/+rNo3T/a/fTteu7cuRm52SDwUFnJnch8XGURNIIhcMIx4+ZMJY1RZaXwWubkeLApwYc6NgFo4jsraC0+1oHBxLZBgCIwAInGICXmRmPPkC7Kg1290uMDzO80PezcHFkepogD2MQACL/1TP4/cyIWzsoPjuH+PpCTi7+jkAHBxsyw/h+MvWiqyoci9lCJaH0WeFoA8iLTsAMcJbLtH9wkMa7O7xflemAXSbm1Woejr7HyBnuDQ3WZl/IkY3mLbjXcJ5Z6jpX5tngnDWbvE8RkFCcfL5Ip2en6fx0HiWpbY0tzInBPwFkmvt+H45k07XN5WYGAQG8zMi4gvh5n2sN79nZlQNoR9ulHJhP/ncAwhzjWGd0wgoWpLsV0+Fghh8Mm4bdxAMySjJxr87suuRXjkBRPoNAewZfd85AlwwnqKDHWgDQYQDQ3f9s43Oiw8xAS04Y8MoOYAOsETQrGC8ITKCthUAKATSCUGg/nZ2esqutHSkzibQvy3UAcfB2CW4dOG8CgNl14n+UAciXOTf93k0HKk6XF+CCLYepqfGI+wmG81rrAH9mad7kLP3kxz9O/+xHv0fHfzl9lr744ovUnaAp0iK6N6pUmsB3OBwG9AD80eYE04+gCgDAsJs4XwncRQk0ugpqHaxV4oIOmbRfEMGep+PHj9jkoDMXw2e4I8BrhRJM2KzoXtsd9LIOFPdP7nKMYH6Rziez9NFHH6WTC3QBXqdpimYDMX7bxpeJHv99CwNQZ61LXLWuDfADeIJmH0H30g8IzUNTowCEWTOL4xZMVNg02hT8L5opwCaulpbAkJ1w6dm1g6N08+bNtLuvUkXcG6+BjuHszgmwdtEAggxAxGhgF82CgUqAIgBKrz8nWHPJOvRXfWYEUFcy4pQ8a7y7rIG5aDfrWUWzDzf9mBf3wTM2xguAjuxvMHe6DZOZ98tzSkCqXw5scC/nUXo/dVODFIFWBMLTaLqEgJlSD9M4P5aRmOvhfAYo23RJ9ve4+zT3eWYBRoLcwE4Wvdd6QRzg9+L8w/NjPtDdHX4EgCjMN+YPAOCNg/0ozVuHFuGUANCdezdZInb//hHPgekMTc0ELmFcBuyOC5CukU4p7c+XBwADAAr7+CIAUHbUCZu4p+WK9z1brNLxkyfpwcef83k6A3UBhp/Bew8NWDRpwnnz0efH/Pt8pq6jO3sH6c6dOwTGeH4sADzAp1HH7X5P5yICdLAAR6Po6hvzYGDWlQfoMktWJ5pRsamHAAev4eVUzWOcmAOjkX5pjAOTEssFAUp+jiX4USoPYHJaSTgEIN6JhEy/Aw3rARMlkC55dnqRhvBTd/clZdBbstsoGFtPnjxJ49F+unHjBkEC+gYByErLGva8HcdED75sa+HP+3xvgBB1v8XvsR5RgoySZJwD58+fx1jEng6AGyYLtgOlpPL3lChwUlNz0k2HvQ7nGUDX7i7AaTTkuuDP+P1HH31IZvZovEt/CecKS+BJfAkQvZAo8pkhXdB25/E2UNAGAsu172uU9rmJ99rxkYEpf748LzzP2tgqiTYgCH8HfuiT2SwdXLuWfvzD76fvfe8fpE5oVc7OTnluoZmO5i0SCTF9eB/GtzuIEnSkwlcsdFflIJus9QhwfPzxxxx3ajuOR/y+GRp4zWfpWjSv6EPPmwCgEiEoVWdlSjBJoR3Js3gu+91Hs7huNz0/uRDDv5OYCDyZoNFfU23xtdt3uE+HHXz/IH36+W/S5AJVT5LpwTzznGOzNyyaSIjGHgNxBvbVcSuaBsIempjQC0ZrPlNa7Ot2tQTPjcovyu5PLbnmykT7l7U0WLx/0ZPmpgFA/ItxPjjYoxbraAQphl5aYo2zu7gAO1RQ4QV/hDFaxcTOjDQkfBDzhL+AEn/4P5A8uHfvLjVNLQlErXN3lw0mutnK9fre+LnGDTak0PQJl/7681cBgBv4UfXFXwUAbAF8BQOwvW/jfje08Ns3sMEArNZHC1MqquoImMLGOM6o1k9T+Rnf50qIuH4GGAEA4i2YuvIBEKi3XlkMPkr9ciYg3pdLTCsKZqUNmL8jHwRXMABr0fHctcy13f43Sg0rAPAqBNYOeaORp+u8CAAskerMHKhowC/C1Vr39BIAoAx4jDMo48V3NRmhK0p5N0otfnsAYLluRPXfBAFrANAL25kXrzVnxn3NFwGAfE+F0G+UCAdQ5VJ2ZFTDkojBUKrlt7R52hu4LAEuv6NkAHKaKgCwG6K8v3znQfrZz36afvXOR+rW1BnSsRqFBtHuMDRJIvBHCQgAncnCmX7dT35ed4oNAAQHL8bUjE+UmOEghcHE77/x5hvp9ddfT9OZNP86Q2m49LsLafVMlOHtdJXRh4OMFwBGlTzohUOdIr90eMHkiH1npmdlURkkUhvEoJREngmidVUaitf0/JwOJoYPB5ZLf6WF1UmrLlg4yi7ifuCA4z7h2Ho+zICknlmUebjkGMAAAbpWwxH4p025ltZcsPTYFVnAFwNWah5GGSzBNzFA+kME+Is0GKhECoEzHcoeNApVdrS/t0eGUGZowpFfyJHvoHtZNGjhz27AYNHmXPrR1kizgwkmDZ8pMwDbJ4iCh0aLzIECwFbc53wWzLstDgjWj8sRURLL+Q9tPDmXjRPrdYdAgIEBwcK+nIvVOo3G0AjDGLTLmcGUxBwAAHSppQE7d/x10GgdNrKOojQTDDqDCXREVp1cjq31H+OWHYyK4bNSScmcDBsEa9IkykBbMC9Uoqp759q1UkbLFutAJvsmmie0nX4xcEr7P+hiP83E+IMDDWH/k5M0OT/VeEdJEBzU8qX5V5c820qun7oiYEuJnZyDl2OAl9eu/5tzXNlP32Nd0tC692LMcpN13yeeK0TesY+h3UVQeHae/vBf/GH60XfeZkfyi2cK0BcnZwQAHeyAmaFEiZrhoASV9ioAnFJLCX/3WvXvDQC65IW2ifu5x8AdGlGwC0+fPE6ffvYZEzaw4whgmNBB+R66X0bTi9oPsL/hjrIX03n68MMH6Ww64X1PwKADMzq6I7u7Yjl+lwGAnP8MmjclatKcFJgDIILMh2At23+QXYTEgcrW1GUQjL9ouuNSdDY0Uimw1zb369rasGIQZA2fziAdHd1M1w5v0B5ibMzaw/0CMMB1suh9dKuH3Wf398tKoGJAsN8sVeMzxfurDAS1dje9OpwzasKlQMcAoRm5ABvUpER2gl1CWUqplwFAJ7TAyLS9ItA1iHEyk9d6omjkgdL/ABJnEUiugimJZiAs1QuGEUr2JmgMEQzAtf1GAoxFUqaWyLEWaN6nDVDC52pZFSUEKJ5uvz3WBZiZ1N8NxAZrHfN5bUcamzibyZxHcwSy29GB82568407LBWDX8d9FNft9YY6e3MTrLZd/qoAIAA63PtVACCOds6TNaBXaz4P/Imnx8fpg48+C2BhHMzUkJqJ9eimOp89OaEUwHym5mUA9G/ePCLjqUwculkV/EMAL2oiNEjf+94bHLshE2uwXZoRlFjj/FuFn4DENYPpPr4nwJDFPO0OjmIGwZhCVqydKLRWmJstlZVEtJlzrdf8CgDQmmidpTQKYc+ePn2avjh+ziZyuzduiiE5lD0BYPPBgwdpf++QpYc4o7FGDAB2oxzW3Wut9TvGswRYJiBCd5JF6rfEjyyzD79nQEBZzEjZI/tDBvtQYQCbpYoNgHso2cb/4e/uhdQHuvDi+y8mJ7zeIPWY4Pnsi08JCI8D8MQ4yn9F52kke7FPG41yn5HbAMD2+ekEDZIEwXIuuoKSlUlpghiPys/wfNXxy0YTCc8t7WQjD2AA8KLfT6fHT9MPfvj99Ad/8JN0dLivfQrfFnIx7v2ZAerQgmbp6yrNlug03sl2ZBH+pgkbMBePHj3me5mwp3TAHgFJjHc/upiPd0UEwDrD6+nT5zrTIecBll4QjZBgZrVMJL4EAM5IkMBZPuV9m52Z0t5wLA3uPvzCfvri8cPweyU5tNOXdiKY2gSFoekYjEcyD9FoF5I41IOWvXRzLgKRcV8+W5iYLnyiGn+4FACsgKIMfse8b5TGxpqZdw/yfuF9BwC4sztOd+/cIRDIeOpCRI+RS4CdQMz7q302NoCn9hHmgec0NGhnMzJ8X331djo4GAcDELqQqszySxqc9QnTJof573WFnSseMuMqEgoAACAASURBVEAal/lvAQDqYA+cJcY5a5MHkOZn3MYApP0yoama13o9uAdCbu76FQFAfKerIsojvRn9sDc1APhH/1oAYNYyMuUye+ZxOQcuOVCMBbMM4DAsRQOAtEsP3KyhZrJlau8lGoCb3Q39BNHVCKV9ZUBxBQBYw2SZYeLLxrUuAwDLBct1ckUAVE7GNjBy2wbhfFTtyKGtYA01X3NbRqn8vtZC+IoAYOkIbrt2+Uy4n7oEmM9SCMnXgeFGxqoCkMzAaRgNbQYMU+w6OuKk3Jy5XK7GIKABADl/0bTjsnHbZNS0d+giMvd2ZJEx8pjA4CLAwYH6zjsfpj/78z9LDx8qYwyRejgmuz0F/jtDOdGgqpMx0FGpGcTryy6JvOe6YxK6VIamhtl6Dmx7yzU7fH3zG6+noxtH6eT0Gb9/uDeQxs0AmbpuujhbEoDrd3fEFol9IKH86O6EoHCgLsE4dHHQ5q7EeQDb+75mo4Hq7wAJAQP2N+YH2UB20O2IrSLAbpVL9Jpu2ZsJBpasBXPOdHSzx3B/DgQl0K4b5e8INAjYcfCIv4Hxg3txqS5LTOhYkvchdl10qcOCZ6AHnSwCXOhIuCLLBlonEAAn840B/SLj+NLhWbACk8G3WW8ReJuZUq/LDGDGGnBJmp/BpXp+ZtyzGqiEqPZKgSccYY5vdxz6aU0Hr9b41Ad4R+WQbAQZJefshtvpMLBBxt3gq8EI/B7AIUW3O9Lbst2lth3mAY5TdHh2KZS6FTai19SGYae5OQEDXH83SnUAOLi82w1kavaQ7KpG1IclGGK4f+k1YS+KOeAyKwOWZi7WTZN8PXYfjDmBw4WAmaUXbErSnDq5xBJBCcC/RWhCApxBpnkxIwB4cY4MPErJdb9wTGVXmhIj/H5ZafBl82mG/iWGrdQJucz24fd1Mqd+rwHWS+1n4QBx7VYOTnl+KEkTID3L4pdp3JM+4053me7cvZv+yQ++yxKi3e6Se3R+MeE4cS0TXEFght6eOi/BmKKYeUf2bhp7YU09w24GMDNj0AxBgyBr2SaMKwIiMAewp06enaTf/ObD1F1PxRbui9G9t7fPfd8fqUR2wSY3m41S1NV8kU4ny/Thhx+mc4ii97oCAAEEUDoBJaYa2XyuRoDhcSzZbfFO2bLQa2z2j+wXgjAyWEDOiW6Lsj8Qd29E3J3Jd5dFMF6U4Rfwn5s6uGlJ3Ce64hKQA7DORJLuCvYRmoCHNw6ZEBlCcwvBXSwcAEIEPELTE2PDdR9j54SU11muLCiG1iDwVptZNCfKulhMTsku4kWNuujSjECT3VZDq5FzgHMp/F8zlXpoyhCJAZ/TBEwYbKtJGa+ftQ0liTGBrAcSMKgsIENJNnTRE6NrEROPlUxGzGxKcfzzs2mA1gK4XZrKZ4ffUCTueSZsUBwaAJB2viq9ypppBQjIknXKYIgh7aYLZNAkSZcMh2MlUIJpi6G6d/du+sbb99kduNNTwmvYk8YXHk+BZZ7R1rS9PAAYKygAVh/w0B6jBuC773IO0YSAc8zSQiSdGgAQ/hdWALRocU+wv7/+4GNVRHTANOqxC6ZfGFdWaHS76dn5VJ9bywc5Pn6eDg+vp84KpaLqoCvATuPeG2ptuEnMdD2RRMZQTbdQcovzazzqEZCEdqDY9QIidsZ9agtDGxD3MepDg1F+Cfd6SKM069TxUZt5Z8bzaKD1KHkX6T+zuUU3mJGrSXTpNQD4LO3t7ab+3gGb0KSuJE6QqMBY7+1cT2+8+WaaRgILCRPuHSTWmBjQhOfzZ4kzF0x2E0s0L04sW4uVzGom1gSU+vn6qKGnFm67FM/N3VbLYOCHwfR5M4rSyPFKCR6WtaPSAxIM/T594d/85jfpnV/+gtI9KLHn+gEZOgAo+1zb7E1Z6aC/OyHTTphfFv/V18wa6N3Gz8B7EBeWrxcBgLJhGn8AgFjf52nI53/1lRvsBnz3zs302muvp51Bh+cr7DZ96/BzAZTpOkqcWBtzZa1SAySRiFhOFmTm5aY1IMfD7qM0HMmCc8VL1w6P1OU37OWz52JWT2Ya705HTUCapmfWxhxKtqcrBuCz81Odx3GOHl07lKbmWhIKJ9AORqKV2q3SqiOQi27J/V5C2x4ljkPfPAgA2Q+sAOpulMrX8/WyP18lEeb1UQNhvv6yuycfLaSSUleaidAcR2k3GqdRK3R6Tv9y0NUZDwYgrx3SBrnC08zDwk+jXE5o+wEAREIV5wQaSR7eFAPUFVOowFSlRBMftOLvqrIpA2DGOyq8sJbkuBwAvASxuURj1uO3jQHIv8X6bfzV2L/xi7xvcwKrmfHSZ6797A0MyOdWfLxOgJT2gWO8Jf4v19o2EJCPE2/a8L9fBAA6UOdnPZBZBDxKJoKau1kCWSHKNoBVKeuXBQB5K4VmSVnSRYNcAWf1RqwBwAxwVYHJtuXUMtYGYYoS4PK7XhTolO/7MgBguTA3P/dfhwF4FWhZ/70MCGsUHO/9bQGAmfWVswyXA4DtNVA7wM24lYYqfyaX+BVBfMGW2gYAco2yTBRaMbvp4vw8/eJXH6T/+H/+x/T4ybm0lPo76p66RBeyThr2dOCgVEKloqGV0gmGT7E+SwCQ2fdelwAgD+aOShoAusAxPbp2nZn4o8MDAonTKUptF2nZU8kKGHhYS2AA0jFPCA4RuIaWXDF4LNmi86osP8HG0FZp1uM2AFblFgrmNI4se0QmnA6vstAovR31lVFSl+AVGYh4ZQ3LzMTS/eGgw/1Kk0qDpK5y+vn5c3UnxMsAoA+s/Ps47CmSDCBrPqeuDzrrGkiE4yMAT44ohfFHo7RMynTDUWKGcKXOmQiO4EjiOeGAoJkLuxGOBmlnd5f3Qq23mQISg2WNZicaVyir5vVEQ151A4Yj5wAUfx90FUDidxhfjI1Lr3BPAJyQ2abAMpkGuwHQN+Pnz+MaOuydbFGGnaXRcKCjSQHmD/dJcG4hFqpLKzl3GLsVQECc/isCKQb3DADmDBjBCGkhkUFQvLjuyNJZcl1gH4B9wRJHFGfGuvRH7JQ4G4/fl4wqAo/BoHODFDja1EacqLtz2c2Q50vBDcc4mcGcu6Vh/wLcQ5kkGLKRWbZtMQCItYH3TSbn0obifuqxKQfm5vTkGUudEDjRUeV1yUPWMRDA7CIScH7mEgDkmt+SgbWdaA3uJT+02ebt4IXrcktjpPJS9flQS0Bsni8NwMnxRhkS9tdKgMlrt66nt99+O9053CPYtpwIWAArUMCXmEiJIvUo4Vfp1vn5PIvgu8QM63Q81BrLTT98nlsTcAWGLpooCQBE4I51dn56xi6RaXEe3VJXtLdgN6A0cjCWBhYccwJ6cNDRqbVghgLk+vzJSfrss89ykydoALrkiGWlXwEAtL0oQUBrxpK9B4A6HHqsYAZeuemNZk9attrLBCAKADDeIUZcTwAaViYDqYD0mooS2VM0t8F5t7e7l/YP9tON6wfp+vVDajfpfiVQDwYI/qW8QjCJ/X15jVNbTD/F8ZiXnEHAcl0J4NTzEEQo2G1M2ERpIQFisPMZ0ASjPhgp2aZESZTsD0TyFzxXvScb7VYlOQC0uMkR9tLkYqYmHrCV6ALu/bmC3e6m9VBaWOpOu05n8ynHAs2moEGG5h+4XzBgKDFR2AJKM4CDHkL/BNiKCMcJBA1c3LM/XyT+8zkawvdYp3gurJ3hSN3PwXglszSpdBZdXJnoCY0idCMG8/M733uTQOBoZ01QZTFVYA8tyVhoMbRXMwBt83R/EZBFAGbpDQOAYABi337w/gec0350883NMAoAEGcKGK1IBAHsBLD1q/d+w3ULABDP7UoP+rAA9Tprng8Xc5QrTggAYp+cnU3S7duvpvVCwKgTqJT9ALgGzUaW3encnifIEKBrZpzz0VSg38X1B+i5GSX84VMmnOmY40isr+U7QZpDrMyhzkSyfPEmAVfoyq0mEDqrWQqMxDH0VQtNT6/zTpJ/0I1uwPjM2flZOj45FRN0uMP53N8/iiYg0/TOr96hv/vWW2+xyznWCTT1eJ04t5AYKM+UQSeadLnbtgH2SGiDaYR9gH6w3GPhB6MUE69x4L8GAL1GzOReAGFnhRuAESW78cL4sumHG4JFKf56ogZR8NGwv3/+dz9Lf/mXf8l9K79YGpi2V60YOVshafW5RLi2X8XbGhZ1XQK5RTuXrMAKAIQGcGnrxJxqB7P8OZcK6vnJAJzP06Q3ls1bztLrr7+W9ndH6ejmTQKAAHab6wWA3UlMbg+H0kJE92W8nMixJIkTESiRZXOzxbRVSrtYK75ZT9UBGAxxSRwIMJ5MkfCE/RxpHNeKU1YLJd8Rd+D9yxT3z2Y5C2qlY57WYOqj+mYI1nk/pcWU+3QWXa8h3aPEXuAYIRUFDUzGVD6HF5p3uq5kJEalSpwjXwYAbNsvrYKXUeCS/6f314mReUeJDWtQu1QXyQMAgIeHN1oA4LCnDtUGADMAaaZ4fE/LT4N2elel2ZBGUefnbjq6cSPdvK0uxPnZnOgo1uD/nwDAbdWP5X7nHJZjUwGeNQBYju3LAIC8/hbbUQKAJXbU+V/+9I/W1Bhy5tBaVMzgRlYGgVJ092y6HUbGKQIQHFRaoE3Gp3xwZ9jdnt4lNt2X6QL8AhFVi4b7u2oA8MWwWHOHNWxRd/FpTUQhVpkpPdUsv8zG5r6tFkCtYVh2JqbBCJF0f11D6X95AFCLNAx6Xepdr9YtP7cWUOFMb3ue+uOXUaDzxig1sor7bGl3FevB44NST79eKPqZA3g9f7skYnMMy/LSbUPTrJs2UEUQgHT4McsN/p//92/SX//1X6fTMwF9oyHEk3tpNH8s7b9o3gHHBM80xQEFxhM0zcJhK9eggQQEXQYAPS90eDtdai/97ne/x8BrcvE8nZ2epWgCm6YrNBmAEwSKPZxTZziiGya0OKI8liWaPWTIcdg3pV8KrNykA9R5eNUqwTFoRP0uMOoQ9KBjbjT5oHOL0svFNEo0lH0f9yHmi4M9NG9YloluvwHuuXtYlHTB0bfGFcaH5cEQaQ7K/rOnJzrAkVsHM6gqpWhKUlXehRcAM3bIBSOMLe/VSAQOBjL/DiRxyCKTeQCNv/GY159O5hI5h3M1mZD1AgYmsmgISPau7aXD69d5bYhLk1MTjEIBi3DM0Q2t0SssGbQlAEjGlDOSAcS69A6BjsEnifiueW3cm55N2nzdaHrg/WcGiNdazTZt5jccz2h+Y6YhmoAQGAgAzFp/+JzAu3kaj6WXRgAt7AcYNQIyxbAk28j6bBWrG+8DQG0gp7Q7DPBxbkUpqYEXgkmh+8NnYwdPXCeAc2p1an0DAFTQKDanzsHY6WaMVqLkBmHB+OL9oFw9gg2DEvi9M5ruVnp+cSbAG+AQorqVSuUmF6faBzMBTmACGrwoAd+yuZPHoaVxtgUAvIr9Z2Yn7WNVgl7bwJpxcJW9L20Y11wKzdHQOM3AhEvHewCQFmk9PWM37fXzk3Tt2jDdGPUpWj6OkqKJu52DKcA5ULfL5+eL9PCTh+n5s3MyZ0a7+5qffo+aT0fXb3BdYB/hXwf6BgTBGKbDBoAWbIhIEDx//iw9ePBBWqwbqQDac4CA1AMc0O5CpN5z7VJJfD8YZrAPD4+f0g5MgpmOnioIVL2OPJ5qxtF0AS7HWeur0oIMFiAcfOvw6Ewykw3nDGiAYOCCBREldM5IxznMPcNgB6ADgAoBW+gCT+AB2qzUuNN+tD+DzL6bnQA4RUkdzio0BcC+G/QlkXDvG28xiWOpAdpwlFCHdMQsGHi2E9l5pladNDW9v3KypNWpujmhfT6WNhR2koHnxTnnYxbNCXyGIXnDpj1hp3ITkKCS9wdVaX6UwDmwAoDBrtQddaOeTKIZQtwWNcpwNrgSIZ4d3RZxnp5enPLcgEyA1zXtY89Nb8IPrxiAjY1ont92jGPY0iItZRkEkHqcwTrzGUBmJ0rpBtIDBGCG7r4Euhbqlj3ogwk4Z2kvzpsbN3bT2998O33zm3e4ZN30wYkU77PSv6Nd2Kwca4LMFwGAIVVBAHCxSB++/xvOq8fL5wAZgNgbPTWtGlIyQD4QAItfvPtBmuP87+/zHJgvokN9UV6I5744n6aPH36ssnkwiAbj9Oabb7CbMP5+PkHZs/YtwaJg7gG8BXAKe8OxZpfpRVrO0UhlmbohY7I37EkLFWcAEmt9SVTYJ1gvVbJq2QwDAV7jZvp1OyNJY3RV8ujmgpDQwH1iD4LZNxhGgiBBDgbnkeQ8CFXOZuligQ7QABN3cqJFvlw/PX78mJpx0DZcoSFB0bwN/oeYZPINrTU5OYeGX1Ol4X3jBDRB9PIMK4JbzqUBC0viWP8tSjNht5w01NljDWfNF5qIcXyXK47DKCo1BqzkWKaHDz9L/+k//d/pKfxISqlAJg7JczF2S+ko75k89i+hR++CpPrctCZbCRaU9svnqNeVP19XVNmvqwFAN+E4Pl2ng+vX03KhJoF3bt/QesD51RPblOMc+woqtbYf9N+DEQiGPec2KptQck3m2QXkbhZsdgd/qD+K9T5wN+JdnsPDsbQ2XZHjc3gaAC66CNNnDMIH5oYMxmlUMo12mHy7dnRIjUH6dot5+uyThzrX5wAh0ZBJfiD2PADAybMZfz8Lf6sHS4w1N1dzMTBuAfhCjEEJbFV04OjEHsa6McBFuxlMSSdCcvO3umrLfm8Vcta9Etw8qp5fr/t5R6XtaroDgBJNEdXg5Pad2wQAEdcsZ9EFGAAgEx4CPsGclN+4nUFn/xnrjP42/CrOeTcdHh6mo9s3dYH8fAaKaxC6/aDGVwYxvy+K2cu9UTMCczOx/KbqOf4rMwCNe5X32LJXW/ph1AA993JNQKsqUvmeigG4DYfxfdhfamCRSKzXFTh/HwCQgXCUIGEg+HMAgWAu+Kb1gFFmYQZgBJ4vAwCW1ykdBRpgaqWUlc5tjaVtGjqtybLGTK2BFQNVGnV/rjx4vioAmMvF6mxNtRFfBADy4MyO/9UAYBud/u0AgPXBVZcA18FmCQBibG0ofR0bUGlgILgIA1WXpPOwaPQzcs3+JVo/2YC+AADUYfmykHHYvUx1bkoRaFADAOz39ihW/2//3X9MH/7mwzSZaGEBAIRB3UvPmQGDwwdHHz0I8NyTmQ4sdFujZlJ0HctAhoHXAOyhfWEnEKUvB3v7pPR//d49aWhAewbMtovnCgiHauxBwApagGAkwrldhYZWdJXCvbL75VAlKVieNjwEluK+cHDz+yPDbQAwMyqo0wIgQ5lolxWDiYKXGARzMthw8CIAxve4A2d3IAfdWmjWllvg3oNNIM06dJwdMyMGBwGiwWKwiKFXslJbDmpHrEF8Jz7HYBCfn14QUOQZB7AVGiHBCiUzaTxQZhnZdDgVswUdRL/wHjQBwT7A8+9f36ejdX52RubP3nivteYAFvB5OBcCg0qNKzuX3lfulux5gNm1XWSpAzKh1GBUgI97aJiXcHJDHDs0E1sAoMsBCj0LBHwGLGnDAOxF52SNUXQfBfsVjEM0aAAYGGLby+WM82PmpgEkPEcukyrXB1mGDTBoe4xAHI7M+XQih4SggsqtKYiPADqeyaXALhXn9TAmGAuUnFDzL+wMugaCEQth6WAciknlSCNKxjeYNXKQSuDGQGa2bdHEprSZs7kYEhlIizmbge0DwOAcotYS4ca+0dw2WoQAoMuX7GmRlrgCACwBPtrjAO+3OWTbwMCrGIAvKnHB+unmgLRhZudnxN9XKOHtpyEZM6u0s1RzFJRugTyG5kZ3795LPbA1CV4BaEIjmgWBtc+fnKqUcy5mLsTcAcjv7O1Jw2xPgCC15LYAgIvlSswgCF+H9h3swvHx4/Tee++lZWceAJmANWg6Yt1Dg4eB1HBEBgLZnZHkwbo9OTnj/T2fqSR/xsBhldbRzAEBWukobgMA8fdSW7X1/kL7iX4Zm+xg/QSjdCAgBxEA5hX2gfbbvlQAa2YkGzBgEyY+j7oCrwMgyJphwaToLJTwUMkwunWLITga4D7QOhDA9jqNbxySqXDrlgIJs5XVIR5MlQDoU1PWzOcstJMZkJF13LD7vCfA2CwBLdrAYBHjPdOp5B5OzwT6I4GRzxzOpRJVaObCoLLYW7juwBLWTC70yYiB/baEAzxirL/z6VKAPhji7NqoslAASGz+4xJFstBW6WIu0G8ylx3K3ejRLRMMl5WADQdSZjaBeVMGBnXXUNuxltZyblig/VOOF9atGeocUwA5YLgPpHkJ+68kn/7tdVRSGBWTqdeZs1Ts+7/7FgNSUH5YcjmbyJ/ITZn+/gxA2o1gmhsA/OjBR2QHI9FVln6XACBZ+lG2jbHD2f/zd94ngNAZXuN4AwCUJImajuG78LwAdH/x859Lz4yA5630O7/zba4rlgZTJmPNhCRL78Gc7PfZKAPNQtBEjokdM26jhD7hTIVO5Nlz2omzZ09jr6uUnzZjNk/D3iS6sSqhRS1NN9Kgtm5IeyRVBqzWskPM1SLmAbvZ5en816xfMJ8Qbsf+6YKVL2Yq7GG3D+mQXjJAv7Ozz/vbG++z7BBNdLBvhsNGI751ToXmJchZ9uW0vuQP6jkacAW/M+AmH0jnxQJdiQsGbo7XVvrewAW1plkmqRJPc9Y6y7kkQABaoww87Ca04eBHPPz4k/Rnf/ZnLO3mOYF4k0l3Ja7AiaoD8asAwFZSNfb9ZpzUVODUIGAJBNZaiRuEElYioVBfL4K6sLPRVGiy3gvG8wX90n/6P/yYTQr3dpDQXqZxGLgsQdJR6ex8oYQEKoI4Lkyww59yibcAqd4SZ44Y4mS/90N+J3pQrdKhpAWGYybSEQsxAQmNUAJOYBsO0q2br9J+4Nzm+qCO5jo9OxXDfDDeSTvjcRru7TDBLI1dnPszSRZN0Il8nfb3IVegBiIch9MlpRUeP/qE+2zUFXP35Omn6ZNPPklPHn9G5uBk+ky+83KiLsnWBkfiJksQNU3emnmIBM2WhAafowo3a4JMrZFfMwENAPYGQxFF4E9dAQAqjomu14HTwL/cBkxpzUu6hfPNLvVYmx3u8xuvSoO0ASolEVThTHpPEVsb8BoW7uvLgIC/bQDQ/k6z/2JCXlAC7P0npmi7K7fHK7Zb/merb1ZIIVjywB+oKyWzzag0kf3+eu5eGgD8oz/919IANMXUZXRbFiwzZkUXRT6sZzqYfx10tywYE3ZAwo9r2tDHxkkrIdhaIO3MpH+nAd8sSZTlae+g7IjGc2QAcEMMPtpOx/tqYGpbG2bdZRzoMT61CGs9EbUGSzbeZp742TearlirzjvEQV2UhMYNw+D6O1vfvQX5ZslbtTMz87NesZf83HYu24uk/P76IBLYgmdwt7xtJkJf2gJ0t9BZy1sr12N5b+5m6PeWAUq53m2AtzFAa6NlQ6dNHow/kgO6qTPX86zWYqz1d3Uwnc766Z1f/Sr91V+9mx4+/ISZZhxIuwPpVt0yuETgDNonKBVF1185JJlxG/sOlH8HumSAJVHjcUCDZQftJoBgOMQAAN6+84q0bJYIPrCH9L0IHvCCeDUD+1o8PJfYNMLyyuB2CZxAdBkvdIXyeNhI8cAIxjAOGxy4o4FKvSxK7fJMHDwlMI0Ay0AmxdnptOr+ctY7gCw5wiphgbPNoLG2B+7SHKWoFn3PIsRFF1feX3R/W8xVCgZHFvOB8XU22vaIgWIcAHR24Gh1wPyClpXsBLpfulMuAp6joxu8DpwNBJ0jR0ohUA0R/qObRyyVQ8mNM55gBBr40XhHgiUA2Py7opSLoONCgaeA2BWBL92rgBJ2lc4aVpvrwOPvbrKIJzDWuAYyqPhvZlChV4mSqRhP7zsJZkcjl/DGVaqEsjEElwq4M4PB9jDssgWdAcK6lFcBsEqkMIZw0FjuSd00NbVB1hOOmm2Suw/y2d2JFd3hRmAyLKldqLUpTZh8yGcgurHDLmXWaWCGlBxBBmHRyICOVjBGcqDu7ybrSk1KXOpp5xwOHJqBcO5OT9JsPmdpsJiuAhoR2WC9YT2/6LXpVLXfb+0l2rYttjbjnrX2iQOyS0qMW7Z4Sxb0hTddMuPXU9rFcZwbwyS7A2ABrzGdb4joq+QajEJpSolhgJIt6jAiYAUIljrp3t171BYk2D4OwDj24RLvoKi57BrWCwCcnSE6Xc4TShux7t9/95fpyZPjNEtRwto1QyfR3u3tjQUwjtREYIgACcDOuZq8PGeZ+SRdjHSew65zDwfrMpe6NeotechaftIldjufxQFsZh8htMtU1tQ08iHwVTRPwpfRwQ0AT7qfCO6iG3u/EyWFIY5OuQKV5mLNubsdHHbuzbAB8Me0R+L8nE8ZuN28dYuMdXRL9/5jwF+B/2Y6eDC66IbrUmVojPbBrlSXdbxYJluyvkP3FYkhBDTPHj1KZ2dnZGxx/IOB2QUywQWgknJoesIWQbsP9mw83lVTmLVKWgEIYL5HOzuyjQvJJyySElHnSAzBLrBLNBhhkriYnM0VOEcCDecf2eOzecgvBGMqGB62183+adap12u5t5ozo+0/b/MZ23tW84NSX7+yLcV6HqCJ2TCNogkI9ou6O2v/we/HGTOfnlMC4zvfeSP9zrd/Jw360hpG6ZzWX3TNruIKdyd3Yqt5poLRGAwsdQs3Q1uADJj6OGc//+TzdHwM8EyBPfwDPSf8GKKx2Z666RTkP3753gOeZ2lwwPWLJi34HIA8/Au7zxLg82l69z3oDKq0FwDYT/7gJywJpn8QAEoHoHmvl6ZrAc4o5fyH/+gfpdEA63CdOkuNx6Cjrr+9FBplTIJjz+h6+RX2TRpmywyUS8t2ns6i1PzkVAkmd+9Ewo/2calmB1mz3cB/JNXZLIwJBjHkRn1poUIDEuOCxCf9h4EAQgfS7P4dAL0ARsUn0HbWS8xdVJ7gOV95RU0aIEVDxn1Xl8fkNwAAIABJREFUDGFrxjUJpuhWGucQ1Np4f0MlDlFBoPXfNLPkPEdXa/y3/F89F3275TLtOTE5V2IJGox4AfiCz3F8fJz+3b/9t+n5swC+ZtIUHA0POM7rfpO4ascf4Z95XV5ip2uN++Ya2+PdDWDAydm6ksxxvP0NVgE3MRfsDMZjsdqjRBHGHQDcP/8X/5TzcXr2jM/Z4Lc6H5bRBRZ2jYmjtRLgkDJBc5h5MLo66x3phHeuc78jTtb8zsh8PThUAmE0BrFgJ92//zXGKwCSyTBHU6ZgF1JuB83e8P9IenncJkgyMU7w+gqzHUx1S6zg6GLJclddfB2Xo1Rf/rGkbixNM1+J8YoELZvBPPw8ffTRR+nRo6fp5OQ0nZ7MmMSfXkSzGzIHO2lvR0na5fJEEjnhg+IcpC8WFVOSauik2eDF/tumRl0++fgfpzPIIEmCQYkwAacYWwB0r968qcRDJOpB1OL6h2gU9kQsCaMoDVQc4xiSBzwbaf9UMYa1AAmWV+/cFUMa5dRgeII0DLsS1S8NYKfERQNYy//oo+Rhy6v0W830b51rV/i9G5e8jAloHCbvDY1EXWnZXM9/D6JD+DObTxEVhsV90l7FPqyB+/J8K/EuMz0vbUJSEciaw7q9rswo3kgI/q9/8ictAFBGVBtQ/91+NIuO+vc1AFg3AfEX2uFB5pOLyQ522qnmqn3juQx5vYm06kJfHQAsv/gyAHBzbW4HAOtx8uc2RZiL4D3Eh/0crWtYXDKLTDYAoIAWL0SN58b3Vwve4MyXBQDrQK4ej/Lv5T2UhqthjjQAIK5Tsxt97S8DAJYBbOveKgPx2wIAa2ARACL3SyCJANrwAgCI53h2vk5/9/O/S//lZx+nhw8fsu08nLT9kcCGmwhQAIAE4wL9zZhpCkbtNgDQ64UlCQEAo1QVDvg0Sgpff+ONdOf27dQPqv0i7gsAoAXocZ1llLaUJdQaR61zOPQSiBZDUQCKgBs6UQQglX0u1wIAQDtaPOgjAEcwrQAtDORCJSYOBOCYGJw00OP7caMP/KzW80oMNBqA2BMCKPP+I6NBhy8d3wiozUzqByvQ4CJiAji4znCu0NSCYvxtg47nL7tfclxCc4egFUvmYO2VKRYraIdBo9iFKqWBw+znV8nxkqWOKB2mlmOAZta8cUCXAaoicOLvqiZO+B0dldC2woFiAAzAHQIaOsZuYBH2GfeEfYv7pkYdAzqUIInpIrYoMsEqUVWjkU2ntQk0G6CYDn+AiMu1tAEdDBospE4ZWBkAuSDQHR1BGXwFMCzHc8KSXZbJIiAPxg91imhj9L0GAP27DGxGFziUZJLJuHAgESVf7syXSyK9zq2B6xK89jlkENDrsAQAPYfSTIumPtTrchm+urGypPz0lMA+moOwC3ZfTBQAgNz/G308awutnxuHahMA3Ab8aU122UWca+EFAGBp970u63PjsvNx+902CcluR/ZhGLYHAKACSDHk+iw5R5OJKAmlVhACwijfhjYnAJrBDscLwNTde/eoDURwNhi3AJYAcAEA5PNGthp7hUByAMvQAsVa+8Xf/S3XnQFAMErIBOhJ43K8owDnBhkNndRddFhm+vz5KQOi+QIskt5/FwAQ4+BS+ezHUTKgaeDj/V8CgGKCacZoB1iS2A87GIFVBQBKQwqBgRncjawM7fhMDCaMFZpUHb1yk+xggzHWGfU6qRPQ/UhAqeHIKk3ZVAC2QJpuNQCOdclSxnNpQD1//FiMNDSHCM1G2GW334R/wWsESwrMY9zrtQOIy6MkTcxJOOi0NQGiQ8qKdjG6CyNg5j7sCxB1V9bp+ZzSEgo2Z2Tm4PxD12raOso0AJUM9m+RsNEebTdvqvfZVQDgZfvPAbaBUNtnvx97hyzAHTFdzWiEjSKAxXMCTY0EkN6+fY0l+3dv3+DZNw8trqZBjhJ8fv22AUAAdNDGc3MxA4H2h+EfliXAAADh8yx70sidR4LeACDOUZwxAA5+/etfk3EM/+va/rX0wx/+sNAUDf8I5xm6fC8l7QIA7Yc/+lHqBIDcCT+o35FdAwDI5OkgEvxFnEM/I7pUApgr7ZXnu0MtYEiJyI8Dg1HnvOwkXQWym5GUXSRUUkD78OLsjIyo+Vz7Elq/AKRh91Ap4m673YF+39uR74bzinYBqoVgpq4Fvi+ncV5m5Co01KKEv9+HJjb2jmYeQJTWteIDAK2yV0rwDJjoHaRRDxra/bR3U01QmlPNfoYr0MSAsw9Yr/dBMK0hvcNzEM+9XJLJjXWN6p0/+/M/T588PFYVx1yaqcP+vmRhIhEFJmx59jWBe9zZbxEALM9W//c25p/GMewOxtJkF3ZzVeJ9vtihD4VxBwPwf/yf/jDdOLqRFgt1pU8LAciUVAl/n/MddmnhHgCIBAaDtJjJPzt5Jo2/k2fhbwYp4frRXrp772668zUkucHGl5+GeQWghP2EcwX6xzzHF7DlvdQPBidL4+kTa1x5ftNXhFak5sAMbtzn2fkp/w5GPuzVcnERGnYBrM8DGKbUhwBAarCjzzQIBrMLVdCEX4j9A0mnd9/5DZt3PXjvo3RO+YhT+mX9HgDJfhoN28B4w9yzndM6nfbbFRz1+qwZb83fdd6dLcRExjlAXenuiGNQA4AA/nDOlAAgrlUDgIhztp0hXAKIg3oC/mEz9vcP0m1UX8BHxz5Coq8CAH3+i8DVAIBam93kx68r73xu599v4BkvBk43zrUvCQBeiitk3CWkoIyTbXzh5QAg31p0/i4/arwsA3W5urVhJl6GubRuoR6vbJ8qfO2P/uWf8hFyaUBozZQaIdppYVgzSyAOnl4jUs7ruAQ4i5XrYDAAuAqkHppcvGxqMoxhaTbBLF64QjSd8amYcwZ6bPxqLZHLSpa+DADIa1/CAKzvc1vg05pAT3BkqvIk+nkjAGpv/AYAhDhqKwDz/MTna2bNxjqtMq+bf7/qN+3A1ws3i5cHg6t2xAmabWF8ytAUJd0vYACqTLh9f5m5Wf3+qwKA9dPX68kMQjARmPmZK1PY21MXwEfP5ukv/vIv0/vvPSG1HqVRCDBGXYnW77OktAHPAP1Rq87zF91OHfiYKZKd5bUCXGg4EVw6vyAT4Ztvv8UDHd2sCOBFZtMAoMsikWSj07gKpl08sDU7ZRvE2gAAqOYXQzbv+P/IexNeyZL0Oixyz3zv1V7dtfTeHPZ0k5yhIFKSAQLyTtAELAj+ZTJsi6Rhyf4NpgFLgKShTZlaKJIz5Ixmunuml+np6bW6lrflnsY55ztx40Zm1qvq6RnScAKN6nqVL/PeuBFffHG+852DRF6VEiSWlRt3bNhsP0ZCG22dBHRwwFlMRd2Pg80uALA99nGADB0ZbsrY6MbSi/KrYW81gBPjTMwpjB8PbNQr6aQhDlRObqJ1hsyNOIghobWYOt8X4CIBQIA2IeaN7ygBQLhaKr5pfNgaRl0iMTtQ9YRZABJnAJZ2Z6PzKVpkIULNdjUlCJjXYspJBD7HkCqxFNO2iA8ppdMT6cnhpXYfXROfX7DV2BJEMXEBb/l5hMkI/07rX7kF4v6ZKAW1uzxoloCSYxM18XBIT2olMisSc50mIgAXs1NmoyGI30eCijFRpRE6Lro+Mwdh8iBdHrW/EQDoRcWYzsORBIdmkL4nQGEyk6AXp7YqAIAGc0uJATMimOiDwRRaZt5P6nYlJq/Fc2rMAZprKQHAnPBE3GKLMpmA5zSkwZ8CUhrGJsdgh0vzRRGb8+kx+kRlMlZ2ODRza0fBqfjS1kFoX4Xygot0vHarSt/AbjAAm31eA0agiu6zwagL4AbABMZpFOsRdjEwJbh6dEkHVx40ZdpC0LgD8wDAqjqoOq7RhROM1bQkK+StH/xHAfShwbPOBTvlKdDW4hxMakXvIL6SQRFM3mDGzaLV1a2Cfp5oLeNeWGu2ZPeLxx8sc05QMQCRLjVgu82HEOMU52im4D0bB5qIBWBQ6PeCcRaaogbo6gp41gIs9mHu+VVzUC8XhASsgUWCgwWY0mCkACyh9mCYg2juxmEUBQoWCVyMWCSIuws4E1sdz4xxAkxugGxTMDDB4DglEIuDvrVEDbThoNjtR16Krhe6LEZsSYlx+9LVa2Qupg5kJpZ0l0asBpOQ0gGxb7vgjQMz9gfsl/i+GdrTMB9mkmk4m04Z57A/cK/ivihAkeMehSLfv/+8GAAMZmPeF9oLL2tUVesxA2PB5CBrxswp7GcB9A0ODsg0AdjKFuqOgLHBQODwag6gdZEuTTrptddeS69//SUess9D+83f7/3GhW0DgBfFMl9TdmkPE5KaAYj4TgZgjH8jqRKacEWx6PTsLL397o9V9OxOuC9Bw5F5HQ08NKkBcE1nq/Tuu++m2fmUc/To8CoBwA5Y6IM+mXncs9H6uF6ns/kZx3F8ME5ff/3r6cYlOU1vogW/7wIlCw3q2MA8Lt2d2eoe557VQoUEXxeYSwRIwPSFJmkALCXjhCBJFHQ3CxXz0NrPsQxtS8wr7tcraLgZ5N6khw8f8N4hPYH18/DRIwKry5BcQLxkKzi0yAC2zyJ/iZZctZg3uRn+lx0la63bfteMLjF4J6NoIY48xUAU/g3Xu4z47vWA+Ml9ILQ4+31pH0Kblfs6i2hwQw2TqIgP0K7H7ww60LjDnzB96qV79x6k7373u+ntt36o71sAAEHSLEAS67pckwYCvywA2Pqs0vwuFkJdWKv/XhN3vA9YK7Ds+GCu3L0sk471lEyy3/2H/zUlGVBIxT6Iltgy3lL1BbEg8vbZSkZnGGfGvYUkiz79/BHn2MHBZUoAXLt5heeRa88cSJduIvOkAcxV2KmyZE48nc44fyBNRE3ZMDui6zX+HuNgzdmzZUfu2YEgqxCtHA6x32aF42DmAdBD3ov9jAWK0Da09IHmFVgdAvAQsS0FwzUfBf2z4xm7eD75/CS9+eYP0o/fez89uP8gHZ885BVagiCbo4aJG+adtKGbDjLHuPLc6591AuDPcdA4TDaBUzsznitNJftYd2Dsotvoerp58xqZrJKAgtJsaOZuVGhoWpB1Pbuadz3HagDw8PAw3X7+BRVO55IqMgDo55HHM+NB7YjuToF9cX6fIhfmwlO9LgAAs+dDwRAv12KDsVR4hwkX+WLa12X95TqPc1qUJZcqTd5GC1v7d94Pq/PevuJ9bRYIBqCeYwUA/t4fqAW4MYVoErxWMNrTEw0RXU4cu+8+IQCYAcUAAJtDx+4Hu6+V1huhJymblItDRz2hv2oAEBtAKwhXE60E58oJm6+3AADzoi+BsQIA5O/kCWBEuA0A5olml7cLXBpr8eWnWlQRMvw75QSrgbJdACDnzX6O82MvxcHybwoAiPMfD5RIlnBwOlJi8NHnp+lb3/pW+vjjKVu/0AKMDa6fTrhRDumoFCsQB36Yc+DwbwadTR5ceYiI6DkOF1ccHLCnIAE/nBykF198MV26dMgKqQFJHEm4yqG9FOAZ/l4DgPnwHsBVU00TcwEgI+d0B61KSzIZ+LmrBsTkgaUAbgk0rVYEtY4OYX7SZesu2jfRGuY4o9Y9ATgMesWBE/PUSZ0rupw/hfa9gTzNNbdGNYxAMy94/zj4I5EOgK0EibzhM4E9B4MBLMNo8abuoe6fB4LQ0sg6cwCzeGCVtg8OAEhE3EplTTxs1gCtrl66LNF03ku0JUEjMsTnZbwiIIBiymTcmXWm1iCPFcGvCgDEmJydnWY3X743ng0BCf6nNiQXC5Ag28SCOm1xCMTnLxbSHtqkJQ99tQmTWwT9PR5XAMdqh5XDr+aiDl8EBoLV14AT+hmuCfMah0wcHNlWYp00GsR06dhrx2S2pcQhBr+PQ3oJeuIAIcah5gdMQJDAYP5iHYEJWBp4NPqB+j3cnzTTtJ4cvsD485okoJjdql05F6OhnGcaBIDvagOmZqEzgwClZ2en6Xx6TgBQB6TmM3ggqROhC0SPc7CJ/9lqtajeUAOA9e/v+3t9MHnS3/P7SgAQPwMAiPEdJh0cxJAE2ABAqNnF4DJZtvCikotxGvQkfj/u9dOtW7fTzStXMwDIOT7o8wDUG4x1OAbpi/NPYOnh0QHnITRa3377rfQxhP+hFbmO9dlVXJBLMHKQ0NKym2VclxmumHc8WB1O+Py/SgCwlXNUACBaD30NpSYaGGy8nlg7jMnQVA3gqQQACXZHi6X1V3cBgJwDMb8d4xuxcD3pQTF/zdLGgeXG9RsUGb9xQxpD/n0f7jI7eAZmbBSjORG0nsHmNesZgM7Jw0cE0sF00ofF74Bhgt+H6RD+DHf33mDEOAKRcR94CSyCTTI5SJNDmUNAHB7Miin2QjDHo7UYrb5kqgbwsA5mNZ47DrvnAMbArF4CVMDvzwlQer/FdUArLmfDAcSWY6H/bw4ku3PNYMp8SQCQ38DYLACQca0oWAIAxLNCgYbsfrCnaBIiEycwlwEAAliBOcQvvXon3b17Nw3JZJb7tF8CAYPxVp+YLgggBgAxvwj0hGQHWoDRql8CgIzpOW+PvRRTJ4Aj7ANvvfM+8xsA/Fgn0OqrAUA8/9l8Q/MLM+DQgvfG629QM5IHc+chI+QEAATnNCs4vHyQXnjhhTTc3FPBgYW2NS2RyKCMwvByqcId1mfZGu+DYynhw+cf57FlxCF3lhBIjligfVy5xLB3yO8bBqDTrE8971KjG/kkitjYy5Gn0uTs8mUC9av5Kn1x/z61dAHiQ1OaeZwBC5gNMT/p8Hv5/aslW0dREJ0vzgl+QyNS55xoXZ6rUG7tZ1yfjSLYgUAtTbO41lmSimuWz1MtyTiPEFC1NjCArMhn9X0GAFHAX6flVEDRoD9M9x88SG/+4C0CLOslWizlSsuCLiR5SomBKLRbCqEJXm3goFm32+fd8vNqjd96GTwNAKhw0WgI03xofSDTuvWUwD0AwLvPPZcggUPznLgfx18xl5E/q3Da6UvyAAw85GGzc8c9FQZeeP7VdO3q1TQ5GjIuDw8ErE5XDzhug7MwmUG7Mbs2dD4CAEjphDMVf2GqxfzfA0DTHDB0tT4xf/Fym7zAXjPUV6nT1ZkCRAkzW/F3FnaRDwfwg7UoM8M4L0VnFdYu8zdLHkE7l+e1AVmin378CVuEf/Dm99PHH33EfU/aqFq/wiWwrqVVa7wF+4THdhcA2Ji3xA6bASpLmB2IALJR5wGYlCBYHIwOGG+vXb8isxIwAFEkj/0sbQC8dilJoZeBwWaGKZ8pma3rzADE2kAh7M4LL2q/nYkxSgCQeUNtSqbPrc/rXwYAfGrwjw9gD2BoabgsZecZVpkPZTbt0wGAGTytEiQXkRrN9eb6+DvGb6LzNTPiK8LbLgCQBcT8+5aK8ucHM9F43e//3j+NnqdAgAN4cMsbWh705OKgHgenvD9H8uX3ZxOQCmkUZR0BOhKvzACsevergcrMimiR9PRsALQqsEJLrdVK8PjMIWscFd+LiVy7sjSfEsCCD2oRUPd9S5OU7WYwQsy5/TIAG+MegShTQjMAGONWIMJlAgimDQNeteK2k8R6Y3r8eNUbTqm5Uf5mAwA6Aa2QZwNZXwIALAPlHgmqrZuoGYB5HmWArf0r+55/DWziiMADJ+zpWUHVhtY7AJNkmd5+7+P0z//5P0/37ytxOexLhHaz/IKARh9VGboMClCbLdBmsVSlki1pXsBehp6osY7CdAFMBnz/c8/fSc/dfS6tFjMmltZCwnPDxpDXs8N+/D72vPLZIoZQ2JotXmKulOAFNk78m6nSTsrcKui2ToIqSOxSSgcHh+nq1UvcqOy8CIo7DhVsP4N4fwApBoLAImD4KYAutoREWwdMJVrzPmvBuK00tDfifpHAcFMzkLJodOKUGAvARZJK5ggr6uvQsEJCb7dzjRcSJI5t1pcJ9+c+Eh604AA0HSTsjAjMSH6uXbtO5zOKpaMNEBop82UGvDBePGxCUwgthMGM0/e49TkAZ4eJiHkN60zfx7YytvAKkHCCZLYd5qJBM3wvtPAMEJsJhKFDIoP5MxjGQRAADMSGQ9vRcyfH5VjXfjZs0wxdLYN+mDNmG3KPjjaV8nmqXXzFMZPoerSm8JrAoolK6goJTgDHYO3Ez80cNOhYM/V8DSUAKIAkWuu2emtCo6gwS/D+aBCvtY7cQrtH8sCVbOzHXGO5hVvr/CzYm/PpeWaikn0TrVHlAXpn5L4AENwCAOsKYwHQlFpq/q5yr/XPGsmHioa94wJ3ud7hbVsMQMRZJJjBXsSBmRpUsa66fh4gv3BvCUMIgBfRSobnAnF3JMa3bzyjAwjBIuhGAtiZUBOScQDMHZvIrMHK1XpFooyK/xf37zHPOFsGYNGT+zUKLfi8zVqJ/zAOoAQRrPuISvkAzJR+PsCSmUERfz33XcxOrYt9lW/nZ2ZnugASgHHLtEYaSM3axOfq+iyf4H3OLZqI92TpZgYfiiJqOyoZo55PXgOZ8UiWeaGtGXF6ONf9mLHlKQLgUQUGMe7g1nx4dBgsLp709HwGPtipLYl7xWbDmHpycpLu37vHn3Wi18lAAFp+uQ+QfSxjE/zZ6coVdTAcK2aOxrzHTWjwAdDDngRgB/vW+TlcWDFfzLQTYLgyKBnzEBpY1GeDsUNosmKfX82jNcrAK/Y1HnSjSJK3+7Y5R15rNnOIeVHvhcxJYqzb+3u4lZe5cvH/Jf5GABDamAUDUHNlnbrjMQ/4aN/DC8AQn2cfB/khCxbYU1dLiPCv081rR+mb3/xGeuGF5yQFsjrL12cAkM9rq2VyZ3TLP0SnQxmfwEDB531MDcD7bPEGszNreGftXDGGIO2iFlO5Nb/1w/ck1ZFGMokI5o+7IZCvYV6czAT4g3mD+zw9nabnn38uzTm/VmlIfa5egokZ5hXiAwBAtZFfTi/daDOMrLVJtjGBr9BaC0a4C7kG8cd041X+xLlvswsXGCjPF3M9DOPycyQAJEYiJDK0FwejzVpl9AVSsQLj99GnnzAnITuaB9V5PPsuC40451HfFw6w1BtW6/Mmxo+dJmGWhd8fTyInwVpAQdCaXFFQNpNoE6ZCaBFFeygKdZh/n0/PFFdDQxMRVEVN/bv3ZLrvRMs8xzXiGE3WwKyPQm5anoU275TxakkQsJ9+9KN3OT7LsE2l6zWYoQWjuXU2sjnX3tbfvIJbE3vLDKAoyOgGqnPkHqDc2vdbJlzRzYNrRWxerEb8c76Z8v5+9x/8l+nFl15i3ouWcJg8lTElc/DivLGYd9KEphtd5c4r6XG/+urXSEQ4OBwHYPyQ+2LqwuUaAPyca2C8glEc4oNMAo8OL2tc1yrsYg3ifQfQ0g1DIgwDCgt4vosoBjNnABO2tp/oKE41Rlxyp3aOz5hfdIhgXAgwZW046Kfj7yo89iIOotCI9867S5ndzVO6f/+L9O6P3iNj9CcffMzvwP3ohf0K2pvK7RtptObxs5OqONDiurK2pQkVmQGorqxu/0Bzfa0OBUtyIO8Fk/P61cvSwU0iYRhINyC52WFmpzgS54vCOJG5c0g+kWxyeJjuvggGYJdmPDUAyDw/UkDlZIpF5Wv/8T/Wq7UB4pfAiG69nnSf2JMH+1xZ7y7W3HMOqNhRglMmrpiht3t/8vPMCgg14Jgh7Sqvqwhc/uaSCc5rqs1rq3NJ3vuL+y+fQef3/vE/abUAQ8uGjAmbHDjh3AMAOoFvcNNgRlSJqs8RjwMAlUjuSXALALC1SAKRLzIB/m9mY1xwBvlZAcD9QKGuaBcAWF7/PgDQFGT8WSK6DQMwtIrWjbZDawrGA986SFTztHT93D2FywDVDGbTo98GcP3uGgBkUDHq3OL1Ns97nyZgfV0N+69hel107T8vABCMCt5bAIB2TUsj6ZP9h796M/3RH/1ROj1BC2g/XYL2FFqWll9wUwIASBZFRy2Z0/mMQZLVoscAgA5ccKG7dHSJiSIC/p27t1RpBTgSrZq4PjkfitFQvpDIMBkoJDbZvhiMEFR7cVARHT+E9Zdo04AI9iC/j0liaFNmECgODFgDdPKEfstQCSEOa6g8bnAI4PfrAAcNQGm+KKIYAHSFDp/NBC4SfIybAaV6jpWMOvwbKs4ZsIyIOgiGEMcoHHKtaydASQw0XA0SQSdgdr3qRQsw1pnuW0xFtEhg7FQp7VBEHn8iWbh0CW1uRxIF92EwWIC4DiVEDZiF97HNAfOmqODy+yIuN4ezdgsw2n/Zhg2B3miJoPYPWhTDLKNsyUELm0xjxBT1a7Gc8Rqu37jGcYAOyvHJcas1SeNvUYyGpSgdl2ghDiCMayA0rnLMCB3FMiHzvLpyFW1m0k/09+D3fW+Y607m/dwxjzHeao1QK7CSrzYrhvN6JbdrMHg8jznPovWRa4q/G2zNiO3WHOTBsmDylYdu/u6eAIWEjP8egFMJADIBDKbGbAqdGTjQBZgbie6FHbb7Ep89LcCW5vDlYt6XFdfykL0L/PPv1WBh63BUjMWTAoBYZ3ihNQsvdxyghYVJGrVV1xnoMcOiN4QupHg4eD6TwZDMsudv3VHLVwCAGGscNIaTA7X8BSDLGEQmgdx9Hzz4In344Yfppx99qIJFgv6OmDc4oFDCASzd0FztFmuIB00wzcCGDWaYIosS7n0AoJK8dgV3ezptA4AtgO8xACCZSnC33AEA5u/pqO3TbDswHD1mZtt4/vt3XGzQmmsY4uV8GoUGU3YjjcKMtAcbTSY8JxV+VBgDQEiQJABArP3p+bkMc3CgDQbg4XgiYH0lgF2aRPhsfc6coAiYemLcdXsy0BoOx4oX/UECC3sdDBO0jiI2nk0RI8Us4fvCCdLtyksCftpHAQTNwPyDucdinh1cWTQLCQ4ennCYjQNpluQwWzskU3TtzUHELqfeB/eEmRwzm/W5DQCWcbhsGWVhJgr2zT7TAKjYy1Dcoy5sSGPQLAqtwD0XY3SYPxp10ze++Y30+uu8RugtAAAgAElEQVSviVG4PG2ZLhkwAgBY5hG77su5gOOB5xslRyJ/KAFAm4CIQa943e3FfUCGI1yfDQACRDYACFfS8vsMAJ7OwbwB26/P/O3Rw5N069azaR7rHrEJsQNMLuxBAEjdNo/992rvfo4vuD6Yfx0cHhBEQ341DHMiF8fA6PMcI8MS7uYFiKE8pNhtnCeVezPxcwFkvUG42rvAGi7SyAdZHAutPnwm9tFPPv2UjCy8cE3HJ/fY4gnpDDChwOC8+cwzzPUACDaFNMVJjBPncLT0n50/YBxGAUDAnPKlIDKmDTsemvjtc6QPwueYX629LExRwCqcz9PJcbjAU+5E7scEBalTqPZsdiCsbboiV+7lTLIpiBfIO957/wNe13ymsyXyBLzcAlzmPgJK4hl8SQAwAwdh2lbG4XIt7AMwSgDQ8jdixuvlwswqHTBenq9QYFyl3/7d/yy9/PLLZMbh/vtB0VKXiOIZ409IAKSNXK3PTmWG8dzdV6jl+sILL9HRdzoDiLhJm86pvncj86tNmik+LHp8TliPYEBfv/5MuM0OCQBiTWF9YR0gZ82F3DB5WgRBIO/TBpqi2GQCBNvSQVgAcBg5Kve7mFAYA2sw68FGjrgGECxTIUmMwBF8yZZ37PP9o7Fa/RfSDp+dz9M777yTvvfdN7keetmEEAAnipaSfvA5wsSBcv6Uz9cM9PyzDAA61x7p/LRUnCHYt1pzjYI9/8yN6ypYbaCNC0kPfdIuALAVY+3GXRaAGSOlq4u5QfOWl1/k8wEAiBfjPc0Rm8Lr4/akiwBAxZnIbXAtdT77cwAAfY7jNIi50nT3tAk4teRZfa9PCgBumXPEwOS16w+upOJKANDv1QOO+BQJgiXF6uvr/MH/9L8GANhmnNkmvSkwxEOoWhEbEUlVPp2oM7NpRaq2+YfdoFaVe89Wj7KTnUJktfxYU3J3BUhTv/dNQG5EgZg+jQYgEwFrNcV8aKPDzTfu0lgpD4egkutVMxk9fnYD9obSfj8YV7tZGLtNU7YBy8czAJ3w7V/Eu3+/Zso1v18fhX82ANDi/hd3jATDNS6kfn71VT0pA3DD1q9uStFZhMQXidmqu6ZGxLf+nz9N3/nOX6b58oCb1xHo2Eh4NnCJ6tL9ja2RwdZClQzJGVSRdTBrMwA9zzLgtOmnq9eupiugY9+9kw7GfYrTIwAgufHvWy/N8wzSbUyALJxbAYBIuVwpc+JrUE7VZjMCdY0ev9KtyM9c+m76PolI6yCGF5JKBto4fGbGI0FBCfWyKt5tABwfgqwtqDGJSnogmYNowcnA5Sox0YC2Fsc1tJ8AJGD8rWk07Es8227JaP+AC9hqvpDYLu8DdxsAV7RwO/iCwWMAkAljMA90qBykDVh44W6GVjdU0zEuYEPg1bhk6oFgPmD8cHgg67FoxyXwUVHCyxZgfD/mIBITsxsMZIlh0GidwJXPax1AqF19zd4EiHd4dJSeeUYteWgFAgg46B1Ga1Hjrod/zxtjsPGwLrJeGDR2CjYg9ytriwWDwQc4z3ewJpR4q4IK4A9zA0mYq564ZrsGl2YeNKFB2z0AdbhgM5lt1gZvKETVsfQIfgZjsgGuxDjqbNQCx2smGNu0q/NjIhAh2StBLxdi6kOtCxPSmmscsV3JZkIHQGMKLUAJtGv+aP7BFe9JXvlQ+JQJFBK5NgCo72v2nH3QZpM8lddXA4FNnI31FG82wNcUbvTvAEx5kDTO7O3HbfEx/pklHq1+ntujrlp9X3nheR5Axh0AhHqmmEdkLfX6OT5wjMGC7qAtf5Te/8n76fPPPkuff35P2li9A8WvDACGhEEkXtAYZaFjteLn9hD/e920oEYkGLX6/az9x9hTFNo8eFtAboxXLszGG5EgFgLTeJfBA70jWlpza7DXLdaS3GjLF+YhrycKLTSAilYfHtQHo5bIfpnQO9ZoXcQ+VrHR4MrMfYrMHQB10epN191BSsHQNhDvg20vmEXLtQoCWNNkvFfANhhDjH10SRTTyfEGcXoW4uR2kgcwSwbgBEz1HrUAccCdh0buar2hKY+dx2H6QmYKpR/YF83vkUvmmgUtjOs5jS/m1P6zWzrWNdSZ3JrJGGjAL0y4mv0+NC4rALAEvXbFAf9+fcDwe9eUZChiSBwwGgCwYcho3TfamHI1hiPwgBIXbP0lQxKAmIAVHDT57z3o/i5SPy3p9vna118lE9fMwMzejudaAoB6Xu045zWCOJHHKABefL+v9cMPP0oPHjwgA1AAINY7TCPEyOx3Yx9gh4P2JewjYAACAJyt5XacekMVxAxQszgEsy9Ir+i54prms3W6ffsWgRL8+8PjU8YUABnY/6XFhhwAQGA3jZZfRDFT+y8AMrqa9jSOnqv9fphfBTKGeYc889Kly7wv5RfIXWJvyi3bYnYB6FRhFS3vcvPGa7mSi3nTYRHfGfkh7sk6mtiT7z96yOtkTAOwk+Zk5wPU+eSTj1O/M0h3797h9UBrsz+UFiDbnMt9MwDJQb9iaFiKhK2miE3tDikwaQnYYZywfnNnSOwLAKARY4JRiD9tSkJpjwA4HQfmSQzAXhSee0lanA/voXX8Xjp5dMIc6ifvfSipkCVcb3EG0zoP09ucB3AsMSf3Ek9iw8qa+s05eRdzfkvDvWL8ZICi+r6t9VKlCYi4fC4b7T/TBZzQ5+m/+p2/n1599RVqa+N54XziOM7WaB9DzbCP3z87R1F5kL7xjV9PL7/0cpocDLh+xmMVxFgo6XZywRtAGvdbFuYR+3UWQUs5mf1r7ZuQTPAaICBOGRkx6DT+drNvjOS4H4RGs+PEKOyMMZ48P2QZnMY4RAshNM2R5xFYk/ZyD5quyGVtYIhzN7X3TpWbd/u87un5LH322efpnfc+SB999FH6yU8+0cf6fE/N9oa4sg7GdHPuaeelWYu1WAfUjDeJZg2murrHEC+w7gj09TpkGr/84vM6j4QWfB8dnnHvknVo78v+vl3naWqVd5GP6nyA77r70ouMmWwBRkE1zlXIG/EqtQAZkx1XfA1710mVVz6hB0S9B+Zcal8nTAUg5vNrgH9cA3m8ZEpUvqzVvG+9ZwAw9nP/bokvtHPiyOtqAHAP0Nk2ytSnO19zLOJ62HP/nf/5H/9vscIMNLWBp6cBAPFFBgBzBSTfceX+G8Df4wDAcqFmjY/qCV8EAFoTpJ4YDpAXAYDbAFjbBXiLwVAlKjUA2AL/+IR+vgBg3fL2tABgPW5bCwygfEXTZcB+0oVd+nc9xvDD39tm/zXunn9dAOC6owMGAEBrS9Adbr1IX9z/Iv3v/+e30vvvv5dWG1HBD3AIhDhzV60XWC+eYzhQmvmF+ynnCreMYnxc6YXrE6o9r778IkV2FzNUXVGlmonJ4taQYCd1Oko0sbG6bUTucFVgixbg3FpSGrOw/SM2qh3mQLzu+LlbRfB+tdcqvmBjMAhljTlrLrFFJFxQ8VlkfPRVwSvBv7yGsTFDtHipjQm/Qw0iCnGLOQdACFUrVNR8wMdYD/pKHCwODfFcbGio1LGl5+ws3f/ii7QIcWJoAdI4AwcnrPVZIL8ReAGscj3gIBOi70rMNb48KIFVMhxwg2YbMJ4TGZorVsDJwAxAA9fqFwGnys2tBADLBNKJM8BLMePEPjSo7OvphomTmYtuE5YTsnXNVrnid/XaZf7/F+Ge2SEDCgenaLmMi7UEAb6HrWIwUUAFNhIw6/gxVhQC875Xm4K44otko2T24UDFzw73Xx8u8LmYAzj45zYpmKqM5frqeWLzE3wGdbc6agEEE8+OxlpHwfiDJlMFAPJaWZhqdNUw7gIjUCEW8xKvGgDUfaM/S9fpxDM/65hHbqVHSz80RDEfkVDL/bqX4Ir3uFeLDRLX23r/BRXUnwUAZOJRaTR8GQCQB+PYUHDg58HQ+0skNmZ2lW7PGncBWJ73AJyw/r72shgKowpApzM3EvpoP/U6QXjEgfad938UOlg6mEIjjAekOJjnhD32tZ4r6DYaAlkQCflIYulodSrnh7UA/YzyNvqEAGB5YOb6iA+S7hCfSNbcVCzF2EBfDGdmOb3nuRnmKmJJyG2dh6GCsQNXQKyrHE+KfZ8HqGBClXtZOSfB3OT3sSW5W5imBMCNpi7GvHifW4oyABzMdhaylI6bUc6WJxZ0xNxgTKVrtNj1dC9EjA4pAuyjAADHk0kaTI4IZuGgRaCQ7vBikFNzdCnTkjW0lAj8aT54nQMYwEEPjH7swbM5dM8WlPTQAVb/QZMSL8e53AJoLby8rzQAoOdGvbbKQnDNXNjXWYN9TN/faExx3uR0oDlg830YOwBTKJiAEQT5RDBMaFTcZ0ELzBBraGEGIvZ2kpiPq9lZOjw8SL/82qvpm9/4RkpJzKPSvIv7cQA1OR4+IQAIgJhaYXEAvQgA7Nm8JwBA7x9uAQYAiOeMFtZdAODJ+Rlbzc9nMFE4QHRJN2/eSLMFjKh67ADAPCKbigxJjSeYSchNAAAqD2pab7nPxLzA/s/5H0wsMNAl7SLgCvmw1rHYxZqKYsty3ltki6YG3TQedtiye+my2pA7naNYuwGMJxlyQZuZHShYQ71eLrzBrIaMn7y3KV5AjoOdBT2ZnRHERMssNfuUzyjPCgkRmCMhF4BdTiHbYoAfLfsCnjQRHTOoucd7VsyZB1O0AbgFcICJhDmwiZZLvx/7A3OQtcZnMwimF/U3kX9NGf+Rl+OeoO327rvvpXfeelf3uVDBGkAB82i7e8c6yl1OTwkA7gL/GK3rFuAvAQByHEHaK9YQChO8nwDwzufHaX5+nn7rv/hP2LoLABAvqJCqLT3yO5wfkG+j04UmGUf8+3LVJ1D6xuu/mt544400GmvO9PvQVYZJiOapuhhWadgbyhwwTO2Qr+G5DifokMK8BiO6z/lD84pg6OO5WtoI8xmqdgKEd+efbgMfgIRQAOoEM3Fdg750ITNjUIVYn2/QAk0QPEx1YIrFglWkXcsVAHEw1UecVw8fHjM/+MGbP0rf+973OIIlsx/xQec9zeOsNejwG+eqnCflYl4FhEdek9Zi4yJPRRyhzACZjWpNfuPrv7wTAGTBkzp9baDNHWJNAagh+FwEAFJKI5jv+wDAbJr3FQGATUekd4o9fz4lAJjX499wANB5l++6Bjwdj/YCgL/3B78fmZUr8AYAgzEVgSwnBAFY5VbejdzScqU+NLI8sZre77hEAwN27TF4VJtbxNvLG9TNRcDOZiNtBpor0PXBx7HPgbC5HwAZce90qVPC6ZZDIPtO1nRJ+xkP9cPAu5HI7Avu/LRIHLPYbjVRjThnpkll6oHWhPrhl4nh7rbbZkO1Bqiv3QDhdutwe5wbgG/3+OOado2H3Wh9zaVrc7l094+y3uWEtXxfc8jYrsDt4zk2vlL+9vY340jRuo/q+Yw2AyX23XMG4PlqSq2i+48W6cfvv5/+7z/5dvrggw8SAjUSX2+ovaSNBQcwtdgqEVKFOFoVQ/DY2iych0ttVnhRS7C7IQB4+/azTDyhjUXAJ8xwXFlH4mMNTm5IruTFQvDBhMkNNsPYkNwqUCciPh9ABY+vcHGFJhmYDpiDAnkk+iuHVWhQKHEyGCXRcLgFKu6csm1ENHIkYtAsxO9BaHpyMGGrJjc8tyUnCb7PF2BGiZnIls+BEm+002Dc0LJBcHCNyrwAF9zbYARR52AXIXGOREItKV0mNScnx/xdVH4BXOo56ODZ60psns0rASDZRRQAA8cTAERszmRDAJgcwnUULoKojkaSG2ub7qKxoeO54GChCqOSeMyzzPQrEsIMVkSyjXvJLbPUw9nOSrO4dkx/z363YuF7pH0mliYOv6ykd7sEoxA6KTIc7eEGbK1p6eRxsZjzOStZwpqRm6+ZbpkBWLuWRstzeaDVM4qDaWjaNLHD2hy61/OzKa8fY4jvVVxqtBylC4UKrSaWqvtqfzIQWALS+G62VBBAX25rzkSlV6C6Dvl5PdftWbyWtilLGQN5rWhBChYgDpoYczOIENsHMMcAwyOATLpGktnZAJOtvfAJWyaaQ1k78bywZaOKj7uAT91jROSKcVbHaVTe9dA8M4PxE3/PGjN+W31/ZdEEgP9mw/b752EEcvNm6pPxiYMfdPmwvoPhSFMAaPqhmDBMn35xP7333rvp/vGJ5ofdXA0U5u+J/CQAnszICZMBzz//3Npifu4ADPxinPFfsgRK8/mct6ULUjF5yj0rg3NsVQ1JAt9nHGBdLIL4up25da0B7hlojUTK6w9xlu8LIMjxBPOELbfWMAPwRbOTsrC1Tv0NWiuhsaSCkhmeWIVkAVuLKVp3S0MiHdwCsLTzZ8QhaoRFXDcgJKAk2B8BYPHAQvMQxf7eRMBIdwidM8RZXa9b4DQm0qDlmDle0AFYbcE4KE2jBXEeTJRFBrBt8iDQBuLt+RHjfj1/c+tvHRHaf0fLZZ47kR+38qCIk35PbbqWnc5za1Ej5qP51RQGed+Ou06ohzZUCqZwL8mxHdILaN0bdMgAzEDPcpZOT0/TtSuXCBT86jd+SWYri5n249i7PO8MADXuwGa+qjDDlsRi/zQA6P3k448+4YEcn4d1DMCd+QVaXMH+jwIYGLr4OVw6oZsLF2C2la9xD8MEokJZSEA+DmbfcrVJb735Fs1hMCdGw0n61V/91YRCK17jAxX5MO8xP+BSDe2sa9euMf7cuixWFIgA2FfmixUZi/iPJjZsqQYwHy2sAVxgXAjyrWP/stRImM101zPO/2HSuPY3Ie3UEQDR6U7EbO6pEI3rJUAE8K+v3ARA3tXLAggnI+k2nzx6yH8/nEDDdJCOpw+Zb0HKiB0Wmx47BcCUpDFMMHQBdChvUBFXIAWuC0AEJKe62tfYeoy9E0wvFStYEM1aU00OQ6A/zJoQT5k/F5plnB/LAB6j0GKN+mbR6Lkv05Rrt9sTcDKMz+qtu+ntt95Kf/LH/06t3Mux8kw+rx47fbzPt/Ortvb0PkJE3h8Ls40c/wN8aLX2VaY/+5i9ZjLtOoNx/4qCyao3Ybw/DwLD0eXDdOPmzXT3+cNg9Cp/GXQnaj2HjM9gkHpDuez20rO5JRxara987ZfSndt3CDxjPAaDcwF5i1NK/KCTBs8e2pXaOFS4X82V83c7kmJgWzu0cntdrkesep4ZBuqWAmOQr65a880IBEAoiaWV5CAiH3LHCVpuOV836tBaLzbpYDJJk7HWkTuG8HlgMIpxiOKXNLRRqOIci1bizVz55bDfZYF2MZuxM+azzx+mP/3TP03LzYiA5WIt6Q+4+rpAzP1uY0BZ+SfMH8vnj/hb7mPYp7Q+glCwFuPPeMZgPMrST3heX//618R8BmsPOTjPIl1KqLDDKwCALeZ+0dnIHJaSPc06hJYt8qXnX3iFzwUdKjiTjwYqDNGnnOu6Psm3M7z+RQf9vDk23THl+GStgD3b5K4zz863RgdQ/W/eX+v1u41LRUddwaMx4M6zmpnKNX4TLtvl9+7CG9wh1Lh8O+/yF3pfbOdXNeGrxjs6vwgAsKS8logtEq7FEwCA7Yeog022165aZ9sTuUiuYpyeFgDcNrl4/IytA27ZyrBrMpYAIFv6qhNWqaXHBMQHnUjAygOEP/9vCgDI+F4IrDLAZXeaAJizeHU7MOwa5ZrNiIDj95VJL7+3wjp+FgCwvI/srhMLuQ9mAaqhw4XaEvtqNfn4s+P0nb/8y/RX//E9sqX6vUuquK5UuR12XfkMAeM9ACCNJPC5YBahUj6XFl9mcvU7ZP4988yNdPnypYTSlCqrGhmxhRpRbSSOGisdfByYMjPBlbCoGPKQAsZb6QaFxMTaYF0xLBqxWrWM4IWND+YRR5cuZZYbXBjZqmnmA2w1MS8WAuJxTWzBom5bP51OIeZ+mg9coKCzIt/RwR2VXACwiyXcehv3I7sQIilREqlWHSeEBgAdIH04dBKJjYsbWKfD7z8/P9PzDVdcmq4gfs2OZdoSCT+1tCIxRYsvdQMpit5nomTgs9fXARKMEwKU8ZzwfiRIZoKiIifAqe025zkJ7SonCCXQxNyI7cZNi5NaYZQo61DcyfPE66cB/hsgDeNrgAmAIhlAgwGf4/Rcbbd4NaCRWmMFHmseAgDUc0UL5kHDuuH9iyFQ3kc+rEfiUcZO/Ztb+YoDKhNGgJECLXDNn3/+BRM5/h0tCgBgmQCiJbOX8IwEUlYM2GiZMICBdWS3ZoMIZDYSICiTkzaAY+C8jk/Nhq9DjsavqsaG+QyuGb+PgzOF6WEUBNdRuDimxlVZz/ZnBwDrg0wrOanjamYVxLVXAGDtUrwFLH4JAFDXE/tHfL8LaNhvW8lZFFQ8P3GwA0Pp1vUb6cqVq+nmpSuq+Acz1YBjfoq9Xnpw/0H64OOPdSgn6I+ylYDgk6KtzdfVPGsVeMr9yIWrXMDKGkHx23sAQBw8yvv2MykBw9Zz2upE0HowAKj4UIh0B3AGAJCFjezi50SzefBiFwWQGPgT3q5W22AeBXCWD/sVAOi53ltHASkAQLZ0QvbBRYwA7AHUCXRrm2FAE3P3K1qQwn3cEg9ut80MNmoGNuYr6/6QBzUUVsngqg7cBv68ZqcAZHCApQOw2nwpqk+DImj/hZskD4+dMJOSuUMzi5s7MHCUW4Ev7PCvM5vKtfEJAEDOBZseZUBRn1tGJI8d24AjbwcAQlA2AFXkdwT+YKgDeY/NIh2g+DLQfoQWYOynKLDdvn2HACBagTFvENvGMS7QtJJmmddzzMNgABvIqQFAvKsxrummTz75NH3yMdrwZMJhxq00FpFLSZwf+/ouAPB8GeYdFQCIPBD3N5st09s/fJvSIvj94WCc/vbf/ttpPD7gPJqFtAcO5nhJC7AfOdsz6eooGOZdMaIw38p9dEETMnUccB9luyTYpAEYzAW8nZyhdfWczHfmf2sBWr3VGX8PjDa1iwVDfQ1Ac516ozAlWMvh2fFauQ8AeDMHxfoDUItc5mCkFuVFkkkEXH+l7SaJhSvXrqar166l5UbFQ2ma4+nEXhfg/MEQgCRoF8pvc2EoiCbMuRBvIl/E+BHwCGO6/nAs4LITOUQwyvIeFgf7zlLxqwYAse2zwJeUP67DfRhPC/nBpD9Ob771VvrX/9efCChcjgOAEcMR+M3PEwDcjm3b631X/HtSAHDZxf2s0nSJLpRluvHMdQKAV66LKd/vhTTNRvPX+82Gz7OThr3blETAnojnfu3mjfRrv/Zr6Ze+9lK0mj9SC324O2Ofxbwb9mS2NZ2eaL9eqjUWnT7umFH3RcSzAPkxPwkc99Wav9oI2O/35ZIN7XMCdhudP7yNubAL4gAKO3idn52lfuy/2PfdTskz12ohwznGG+xJ6BxCzi9G4mJ2znifllMWGB6gXRyGbWfQLZwzzuDnJ1KoSKukgggLYME41D80BTT8dQPppjDtYe5osx3GNeUdnKeQEcS628it3kAh4gz2IGhu44z16tdeUUGLBiRL7quM12G2GJ42GVfgNQQRhcs2yBjQMK8BQKz/F158NQOAeP94qOv7qgFAqNfvnOd7WoN3vvlxP3xKAHD3mlOB06+LAECO7QUmfQ3eIGbqVw4A/o9/8AeMjE2lPQCDzOSrbrVqWc0MIAM5Www5pxARuCrNGrul7aNyOtHSxCzSkYIBUL6nZig0DDjdx1araDAl8P2kEleaEwBeagDSI1ICbbUOXz6ExBfuQ6KhacGFlvNrJCWaRTzQZwp4CWY2QtAe//LzvwwA6ITT97bPPKR2Z9uy9a61DPP0CUCqmh9mAO4C/Fz9qxdba07UCXIhWlr+Xrltlr+P760P3grEF4QQzBuKE2szGQw3ZOdMLo+YGPz4g0/SH//xH6d3PrjPytC4fyRtnPksTAkEqjEwA2xAc2rltIvPBfMLAV36aRuKz6pCOyRodOXaZYraHx5OBHJ4fHmgKkc1mGvWyIobdIXdjDJuPpXwqf8N32+QydR6VEIJhNG4YpkGkwGTWwpeL9FCe5KODgXCIdGgrs5sxko7NdyCFeVncgCQDFpC4U4LABBGFth0xUwZMtHA5ZMl2B0E0KREOgdfaDGSraXWGKxvjJcTZzN83ZJiFoETDG5g3W4aT9S+A/AHIODsXIkiAEDOk7VYHwTbCOIJGMD/S3cvGNIBAALMUWzQRjwYqRJK51Ak5MOGTccYEK1QSGYMirl6K4akEiRXLv2eMn4oYTAg10ZwSte0cuP3OOJnbiHGzxZzJX48JG/W6fxcCX5difaBRaAURIPPVVkf6vn5ei0ezDYntoWrso4/s9NgS0OsHSkwXk37GJJ2AXwAVhGTHz06TienJ2zhRoKLZAgAEBI5AZJx0Nmx3MuCgxmBEqxuXjg4XgQA6mDTfmF8vJ52abgaBGY7E1rfkdAGywhrDcmp1pEMg5r9pvkuJa763j2eHzuDXBvAajMAtworTwgAbu9/zYF+10U4XjcVZO+LDROQ8fEpAEC+nyyPXro8OeA8/KVbd/j1jptuxdScXqeHx2fp008/SV88OtZlxnpG3ESsnBct+iVTw4zS1lyJllt+n08mkW/UQF7d3tkAgP5EjcPjpC9KIFQAuMpCdhXndRQdGViT83A17YX5gOco3svPgwlBAQDm7wDw0+uRMWGWnq9U8SfiVDAAPR/dApzHyb1VAQiO+lqfdnVtxTfE99BcdYHC+qoEFdCSFGySDGjki9J+BGkIAiw+WFFrtps2FpcvgDG2OAHgDF1TrNHj+UzFmoUKBG79dyvVgiZC0m4lwzAYl57fW3lPAGtZo2pvPrVz6eZctcl724WSOtbkFuAt4G+7wKJ5q/ZaM+WXnUbmgK6cBozGYxa8NmnBdQZmJ2L6eCTACtpaYN78yhu/lF577evp6HBMRs4gDnpg7FMzL8KZgdim08hmTQ2DWvmYgEbPy08//Tx9+smnbLkTAzDy6nUUHkO8uU1N59AAACAASURBVAYA33rnfTEAV9FWFwCg9Zoxp7F/QPsPzGAA5wwP40n6jd/4TTHoev00Dc0wAuHIPyj50GHO9sorr6Sr45NgnYMlBbAPhmkDAgF0xe26RVvApjuSbB4w6as4BJMZ7bUBdLmrCUxc6NGSXbdM0/mSOdjZFG7lKFpLRmOxRBFSILbzCdyPzTkQN818wr9DM09j0OPvTSZHchDv9uXGTVd0gUgCjnVd+frQMcF1JoAVnRrcm7FvgykIRh/pt8EcDDMuHJj5GXH+W6xk1oP4iPcDINTcb583kXHpfOdeSwFF89lpuxVzM1ee0EWnwjAdDCbp7bffSn/0L/84tOqCuZbEUgN+8yQAoFdrvX825nHt9bzrvOiflXHdLaT1+bkBANv7t9/nubIcDHm/54sO2Wsvf+2VdOvWrdQZPeK8PBx7HKGBuaIkAvOtbkj5JLl/r1fSejycXEkvvPBCun33Jsfv7p2r0l/uoOjfIxCNeQTpA+5xfT2P+VwdOsjnUSBGzo6cn7rZ1CoUYQLAvYATt9KLcIECAs8gK60va9d6HJCHU6vOkkj4zsU8bWboHtLaxbmD7fMs1kC7cEWgj4Xu6RnnOfJYnOce3n+Qzs7OUnctV+PVTK3Ox8HcTQO15n7+UK3/a0qFrBO4jHkvDFdh5xrKv8VYLZ8179uFzGh595mst9G4Y3/BvYF5jXWtLqh+eunlF3VfAaRi3XIvJ8EBmtjbhdtWHhOMQ2uPm4k7n8O8sp/uvvSS2o4XYBxv0igYmpZW+ioYgJwncd7mtZcySJlQ1I4vlgq7yFjU5yvO4RaYoXm/5Sq9e9vNz6vOx0wgqBmAXsNPAwDy3kOqIseTqvPEMVYmlU0nn9+/xQA0AJjFdEMTA6Tona9fMAC4Z7xbbpwcmDjpPC0AuPF9BgBILayiem4AsJlQbQp6fX1bh7liRtRJGX73cQCgIut25d+Th4EiWjAdMLY1l2LBt5x3t1uAuWW67YQio7tH/iIAsEaom095egBw77MvnndbTHO/+G4+UFbj0DAI26n4kwCAXGzLcBXsYXOYBwDYTW++9T7df4+n/XT//n22agBQAmMHLSWdDpJgaFvoWQAArDd9BvYAgJSUoV9ND8ZgykuvvsgNEwAM2zNN3Q7mgceQmhP8DLeb6k+YVjTvQQW0DQAahG2DyhDHlWD89FyApF+D8YCAH0Aw6cgZIEKrh9pYCVzEwRkJEHXcCCghgRCQibHCgQqXyxYafBccYDddtgJT7IamAPHdaDGJFi+NozZStCRLjwaHCVxzXGlspDwYF+vdTEgzTrwm8JkwYTg7PuFGawAQFWUKYmdHOZlr4Hrx2WAEMAEu2Gb8zjgYAqiihhLbslfZBZIxjQdNXS/d6sCMQeJUtLZhA0cC54M6nSzjAMTEISquZv85djgBxH3mTaOl/RTfu5bjl3WyoJHnF7VYQrzZLRgcBxpoSBOKycFymU5OH/HX5LSIpFOJmplXpR5keY2sHhdM4jL24edo4S5BAbs7GlDBPOVB7uyMCRv1NyGYPurTPRsAr/UXDUoabOcGWjgVi3kRGkwxkbYlJ4I5E4cxG46Q3W3WT7SseNzL8TczE+AC1g/HjK02aptCez7FpqdTVt7FKC5a8yyR4X3DCdLjieutUFseJEtNOc6xigFos5bMFWIFe3uP3D7QfLUAYHZPLlyxOa4FA5DzKNbKkO1ovXSwSQQo0OpGYBgsj8WCoDEMVxYrtL1PuU/jOeBPiuKnDo0g1uGI6UNgHsgdlV3N0zigPwYA5Pi3nog08sp1YMD9ywCAii0af2jjNbEGGqoqHCFe+tVec2ZGu5U4GEsFAIh1L0BOwUux8ckAQECUnCsBAErLS/Obn1UU+Cw6jviOdkSDgGV8xGeJuVG2sjaam3I1hIlVfD4YgGBDBsPbIvSNRq7MRgjmLBfpLPbBzjLuj0YkaCkODatgRMo8opEQRyKPF/a/8kCv+doYuWxLlFQTo/qr19nPGwB0ix4YgJ6X+G4w1PAnDu5YT+iQJ6AV2pJXLkHjq5+Wc0l2vPjCrfTGG6+n28/e5AEa8Uz7CnKcxwOAmg+NSDsPrgABo5iEcf3ssy/SJx9/TFNPgADojdBECqmHCgAc9sVsBwDIIsum32oBpo5sxH5c3/l8nj744Mfp+PiM++5wNEl/5+/+ncxismlC1tbdyD0U+r+vvvpqujY51bxdoTVQIvtkNiXlSRngQScA5nFIFBk4Wk4V/ztga4W8CNc187g1pVXwPHDwJ9OdgBzgAOQcWOcCEObQ/4wuhGYObdLJwwccj7NHD7l/LudT7peTgfIcACXM5/qSUgHAKI24xihAZ7PQ6HQHAu4D911U5smoDwCXmm0A3H3Yp/txN3Uh7YLOiqEcT1PvcjD4o0ARbsJuIUerqAEhATsh8QDtQRqOKD/Nbt4ptAxDugYtwJDwAQCI+bBYyCSu01GHCm3lKvBVE6zdApxjaRXY/RxLwMcxc3vP3LH283m9DfTVAKDzQwOALiKfbcBqXKZVR8Drf/qf//30y6/9ckqDh8yTzh7d016xVqdGLlSFmcT8VMWA+azL/bDXG6VrV6+ly1cE6F6+jI6dYQYA+x2ZwcCMC593cPUoWN3ab+Q67a6eJXM0Mt+QV5D9F1I0M0nvoMggADC0+buhbbcSoHh8fMp4YqOc/ByisyqFlI/zLukLIqYJSP7g/fcIFMq9F3NY63J6BgbgPG2WMDoZM6/A+QDAIAoZHWiW9/rUCAYACAYgzglmhON5sLOrNqHIUh8RpphPF0BOAQBq/w53chiLLZaJLcArOY6jRfeVV17SeQCdYYjLwUBEpxilhLwt5jyxPUFtlmgA0PHMRIq7L75MaSsDgMPQGv6qAMBGPq0pbF8EADY+AZIXeNzrqwAAy3X6JABgud8/DgDkmnWLsAu1Vfxo8hrtw/la7GJd4ThbAOB//3v/y4atIJAzJYOozQB03tS4q4FGrhZCRc4mUeRfH6ORxwusKprrooK+60HtYmeV7/OBpGR17fqcTgWI+D2N2GUDYmgkI0EuQDEuuEjEm4m5G2DLoMJjMnQBeO0JmtlXFZKZXQ0LoE7j3WYoPi0AWJtIPqn7ra+6ZpZsaV1UBiGujD92Ve74x33Xhet47BypDmJb8yTbqj8lABjXyKInRHCTwIV+X9pyf/btH6R/+2//ber2r6R7X9xLqwX0M/rYBrThrU+1fELjRO2428ArkzeIME8FPAB4QuKMTQeVutvP39SGOZVunE0kDFx4bNxC6spR1lTJYx3U8nigWVMC+mP5AIdWOYnFsy2Yh+M5E7LRSBuOD2EE/8DaI7Nxxfeg6o0XmJLU/VkteR8Gp7yhI6iNegO11kb7I+7t/Ow8t/Jo7HRY57oE4y8OnuUaQPwn26Z6zqzaMZFv2AJes2WAlrGLkl20DJweP+T3SUC7y4T47PwsA3AYf7zW0MiI2EaX4ajI5dY5uvr2UmcgbRC/pJMnoWMllgHULtVqRt28YN9xLoeGkc1aAKy6BZtzh/qLart2EsgDbtYoa+JH6yAa8xHXZVdcJS2q4OKghQOLTQv82dDkwmcjOcYBEGLSAM1gRiJwUJqPYhWKnYnv7UNrLDuzKVkhwzMzCKQ5QxF6Apz6vU0fzA4dmvB3uC5TQ3GhCq50I/UzJGbIIHFwwBheuXolHR4KYLRLdhlLmHCGG3D9Hh1SYR4SYs7FOtJ7Cwdutl438UUGL82Gndm04Yrt52M9Mc51Sz9AgzJA2dl8lubnavGimQPF5MP0Ir6vlZxUjGCumx3UwPIAWLswqrLYZCHNnNmuJHMdFKyU3TG/ZsDrXZnpY4ZfpS3sz8rMuCiMNAWoMGWI1jy/D4wzrotIkCZxsLZoNY7YmQ28ADMiCqERh2YrMHT6qTsaS+s0A1NtBk697WcQjWYX0FeoWhsrqYxc9iu0eHDPW5qdZsTsHtwWuKTvbWeEaDk14Cd3PzFjOgAIABoU7FLfgyQoALSILaEJqIKLpBvE+iEjKOZAlpDIov0xX7LGX7M+XPHXx0quwPNa7X+NkyOeI2KI5BbksFwWdGxWUP7MEgEczwFa0aRtxHkdGml+rosoeIgZLl1QFC9waGbrIA0OZN7EwwZiNv6DxAM0w2L+bZnFVeuofC7OUQSk1K/HH2ieFgDM2pqVBrcPCuWewOdhF1wUowCGwcyhAN0ygz7kFlBgAThLaYJuN12+IsdaMGrY8tvvpOfuPpd+5Y2vpWeffTZ11wKUABhyT7HGYhTmDRCbybILAORzDcbl55/fTx9/9BG1+kbDUQYA12sxGcHJ0TwLDcD+iHnU2z8SADjdAGwaMk/nvkeDArViAdg4PpvSFfxsqv21N+ynb37z19Pk8IhrgW2CUfjCNaEjAfH7cHJAs4Vrh3POZ0uA+GlvNnMBHmkm5tNGBTMffqWVh3xQAAALl9QCi3NLPE90THB/sdkOWawwI4lCMI2xGokXslTBvo8LgYkG2+ex5gD0nML0RC3cWANoPUbHx2hywP31P/7gTeZ0B4cTGsI8OH6gGBBGIC4gZi3nmVpLO5sF9wraOqDzog8N6T5NPCglExqR2QU+ir3zdTAAUSjg2o8uFbeMLsQIBXMSn9eHJiXA6YE0F68ciEW+gWYd2DU08u7S/Zbr/Wye7n9xP/27f/MfmP/NF5BowWiruDnHOi4AQMepfQBgvZodd7YAwHAdL5vwVYx2gU2RoTwmaq26hbzNgMwamuhoy9e7TvNhT8DmUp0rv/3f/g6ZqWfLjzVugQv0O6E1H/Nq3VVL90F3zLx8NLzC+fDg0TxdvXI1vfDc87EvTLVfrtTqO4p9F+cYFgywsljYlkHdGqw7yigsmecdHIpJTCf2+Tydni+CQSztdM61CfJM7EXID6ecR/MzdUmcnSqe9HtjFvpSL4Ckzpzz6uwRtL2jnZvdWFrHkC5CgXUxn8rAJiQC9G/Se8WcPupLZmk1m6Xjk2PueWj9RaGQ9zeYcH4tNnLPZSHR3UMkfqBYrn201LdF7kqJAmrUbut35/mS8LkACHUuHI0Psv46PhNFBq6/JeLIOg1yPiVAvdl3dleIEScUh3X9kBDAC0A/Xreff4EST5vQROzHhJQWI4DHPYlJ/PgiDcAmf2yurwUAFh+vOBku4Du+dlenTc5vIw+pQfd6/6s/tn7/PrinZAA+CQCYP9f4xR4A0JqROb/M92Gmp6445x9ev84rAQBywwyqea+jBQJIwzbRGtQ4sOQ7/JsDAF4E/mmD381orAHAXDkNZh3bZkoH1AIA3AU81ddSuxJ6AuVJYPevOFQ14EUE+CJhL6/Dn1O3LLeZWsWi2cMA/HkCgAxeFQL9VQOA2xTjaonuZGIUSXTYvm8tbBeKd7BZyvdC1BivxeaUrpKoFEEL4t/86V+lP//zP0/L9YSto73uATfC/kYbTHetlki3LPm5lc8PcwTBHQDg7FytpodjiPF2mXQ9//zzqTuEBfyCGkRcx1F55MEnKuEMJhV13IlHE4yiRTgAVZmRNOCYgqsAQLM58H3QumHhABpQAGDm2HhBm4fY7yqtQJFMic5zN25c52aKhEFtoQKB/KI2YABcaAXh+qIGnL4bG7qZKfgOaSMqKcZmIy2QRiOKn10EUK9Xg3+Ma6sGOCHAlLWvxLrDy20BSEzOT8EAbJybZ2AFnkNIG/py/dzavEY1LnTyqDsX7a/WkErQqkNiUHB9kJjSSbDbYRJEwK+nlmFo+2B8/D1+brk1InT9AACyZTTMMZDUsWIJFlNU+MsWPSfifIZkvYQ7ngFpgAABrKoVAlXwBRNP/BwMqRbIZJfnvrQa1xtpbAH0VXIsANAmIIzN0bLBFh27zRWLjNfmtsNo+8wb90DXjecPQBfXI5FuVfsNCuAa3ArMZ4pDxniSJocDulTikIfnzXkbh0+8x0lF2WbsS9N60P3zv6IwVgKAXi9az27F1tzVHGy0EsniMDsWLYrRGm4pCAMuaFvBHJmeyZTCrsAGD3Gg5cbfil9lkueD4p7ELzsztgtMTui2Dixu3aw1WaprqONsI+7f/penAQAJAmwlSNEilPef2E+t2UcAZ5UmSMDJNAkTCLhMIsmHyRCeC9v5V2kdcWGxiTXSD+C41Z7etODtSgQVqxoAUHO/atGKYSCHOZhkZc6AcakTyDoJrcc4J4eYbxUj0gw4a8zuAgDrnEUgQbTUGciM5465a7Md/57B/HIfyrmTc6taCzLGyQxhznC6w0cBIj48M5sot6BDudeQ16/XWTlnvaekLkTU1QIswEX7OaAIHuCC7QWpBTOF/Sf32zAWInCMZCoO12A24fsBAGoTqYDX+HF+dlvAbDASq5/nwvv2Qmqt9ydlAF4EAPLwt0tPkhIbDZDEsSBTR4AI5zaNINB6hz1M++zhYZ9FGewD2EfOjx/wEP/a115Or732WroMeQgyjcG2Xu4EADmcjm8FA5DPLSgqBgDv3XuQPvrpT1sAIM8y7mQyg2oHADifzdPZOljsaPmvAMABTbwW6ez8PHXJgFuRAXn3zh22AovtLoap5+CABb9emoxG6caNm+nKeMpxwZ6NP7UfABZYqDDYCckJX6dZ5Gh3RWEp7tdR3ABgL+YxNI/5igIJTAgIQMTBHowk718c19CXNvAPoIwARBz4T+4/ZB5y8/pVPtePP/08HRxMspTJT376UXrppZepyYZxqcOb3U+xrPi9yzBKW6tIOT19xH16fvaQax2An4oH2i8B8OH37Fi+SvH7EX9QYGR+F+fV0fCQwKWlelahRYf9kYDj8pE6NtzS2pcUCQBAXM/hUCZy3/+rN/n3xVKFaQCAALYhYbXF/uaeJ82uMr/ddV7MhYdqPRswwP202xjzk9b114ygLFn1ZADgtK8OG7j4Yl3+7n/3D9Kd27fT8fQnyh02C47HqK9zhwvpywDOB0vlmuPhVY77w0dw/u2nO8/eSleuXEmTw9BrXjzk542g0wcd3jA2W0L+D1p/cT5Z4Pxwfk6GJwA7FAqQZ3IKg3UNd+jIszyeivnat1dLGfZBGpZGet0D/h3zAHrgAHrx9/Ppg3R8fJJWM8VlnzuGo66YnYsZW33PzwFcdtMg5CS8D1lbe332iHFsMoh9Y71JP/rRj9KC7K1uOoYdfEpptop5wg6dplCLggHjVs63G+M4nass3dTkFo7Hej7qnErRGXRwdImfh9iBGIh2bDy31QKtyisCgMylnUfsaCEtp6IBQJqP8NnJcM4A4I3bt9PVq9dYQGEhrIN1ivGS5EjDed+9YX1ZADB/WtEu7b20JkGV37wt1Rb51w4AcB/4tyvfyt8d+15rDPN83fZwAEGm/h5/Fue3pe6MR1XrvTSNYfzOecUTA4D/NHZrtdB1AwDsRUA0ldw31LRChPZGbgmuI9juSqWRSCfIdhNufrsJcEwiMkDgz2t/7uMQ5jIAGwDc0mCw5kqu5DdXsgtYLBmAj2OeNS3J2+NQPnAzAHdN2m29lvbT34Vo18xE3019L3WCmJ9vdWDKv+88tl7HOzT33K5TvjXbiz+hps0uxt+uzXQvALgF/O3rgaueT6VRWQeVrcUfGxcSNbdufPbZ5+nf/9l301/8xV+k2VItFpPhIRO/QYgMwwUY4IQp4XkTyAfvqNDyMLpKWJasuI3HZNJdv/4MQcDp/F6wNjTa0iRTVR4bMgCk8mUXRmIkJbsJB9+iegCxeF2Tkxi1NPg9EM/FxoJNGhs423XRGsUWl1KHTJVCUNOR6Gc9tG6HGzBaM62L58oY2XVRMVYMYN0/u+3iupG0EJgKMwsCa6hc18zZHXqGvK+4V4xTCXQabMJ7mGBFpVnJ+ZptDnQlA8MMLaFLGGGgNVlA57Aj3alEsWE5fvHaBjI2gWmHmDPa+KfUgtMYs5oJxlqI/OoAq+eC7xKo27AFkbTMF2BFBogWDrUlAIjkDL+ndmy1H7ViTcRvg8UG0zAumkdib6IiC+AWJi5eA7g+H5C3t3fdE8YKoBRawcV2FTCKa2JluHIxtUs0wRJUjEPjiCLzBZhlEf000sHdWl5goIiJK0YQmIAYf+oxgdERgJkZluNJny0M1p0pD/c5JoYLaL32GcdDW5GHFI5ZsZMVwCBBagACdKdD9bgBkfV7ik8lAIifud0QlXG81HonvUkeoKenuSVNwG7TQukCAn7Py6IyIdvJAGw/y0aUnvOsZi7lN7cZgFlHxkyTLQfq3fnBtnlTOx9oGHDt1sNmn2o+l4fu0lUd8S7GES2bTIgtsh3aKt6bDSC4nRQHNTIU+ojnYH5JD833iSO7Y3g5b1CZ94uxJAB2rVm5CpYv5wP+PK9tSxpkjdf4JX/+40DAVoJpfWEn/mH+wXtbr9NiLuYBANFyPpceOQYJmC921VoIt1LNT7XjumWIcZSM4UbXSK2augEUSHXt7efM+y4cvg0ikm1BQCAObtYUDJCFDMQhig9yW89mJqEhZLBD7F7MgV6wf6OTIOIhxOXxWpD1AIaVrk8mcs0cA+ude0bd2gdGFtZ/tCC51TEzVOP9fs672QllK3B7Ve7/W7NeyxzPc6AuGJcaYoqvYlDmlugYVwNYeS5HoabjFudmlqsLICRNzADsDhT/+kO5ww6HAFB6aUlN4Hm6emmYXnn5lfTy888xr8HBh9IT8bkGyrNWp5nLlQkPADGMOz4b8xQA4E8//JDSH2YAlgAgzAy0cHUQG/XEAJQG4DxNV30Wh+D3jnngPZ0mIMMBtfQAKBwcHckBOqkQiHmC4o1ZPGALck7TUbSXNguZETxzaULQGrIpZNKNZWI1GGIf66deNyRUkpmEao0Fk115Vugx9hvmm8AE5SuLc0iHdFNnHfmbgbKu9vFN1cHVMEViv7FmJV3Me+neJx+zwHvn5i2OxY8/+IhAz+GlKyx0v/mjHxJ0gBmEAkIw6vA8WACLolesr7TQOgYDkHM0ngdasbF+FzMx8fBcMb7LJWLUPBdE790/DvkUaReiIOoCHk0hVioENe7QWngr7sGrNLRJTzLDMtjFK3QRLNnqOZmM0ztvvx9aiQKCVxsVnGsGn5eBO2xaYM2ObiW/rzlXN7GlFdM9T6s4uVlGjKhaSX0dpYa9riXy+sjvjmHWhmLXZkjTlt/5h/+NzhXLz9RRtFH+NkhwfW46afyc1nO11y5X0p5DAQXjc+XKpfTSyy+n8VCAUJrP+Jx6ZOCv06iHcwM6SLTC3aoJN1mayUShGO671mrF+/o9dd6kjdaTJRqwTvC8V5vj0COXKUy/c5mA7WR0JHfqjoDd0/NP6dbb2cilG/sPcq/hUEZx6zVyq2nRYRGa39G5tJiraDvqal1OohD06MFDAoCQCEF8WPTGzNXmNruKfNdafygs2hVYxd5oWe+hS0XfqcJS00VSnoMZAZBXh0nkcHLI3zFxACxjmoAkadSiBZsdNAaKcodBwEDuTMk/D8kDyjcB5EOBukdtU7yu3Liert+4IaAe51QyRgUA8rnWDKO8T+h/BhdobeWcvzjP59ySzLbdHZTV1+S/1vusAcrH5U/45QZYa2/0ZZ6G92E2l3IjzZk6pAeKX8dzxT6Zn3FcpXKUqqOmKHCXz79u4X5SADDHh3/0j//JxoF3FwAIFyi/hDg3SQl//jcUANyqgMZ9fBUA4D6QrZ50TPAr3Cm3d9jtbM8CeBz4tytZ5OQrDlk1OPmLAgBr8C8zRyOgPKmmzUUAIKcegJN96va/AACQiQVFj1epP1DLX2/YSz/96U/Tn337zfTt73w7zZfSDEEFDf8OAJAVoLWcSaMwq6VUtMs5ccCfaPUb9kYEMYb9frp1+3Z65uazTDAX64fRZqJACFMF/I5FdN0qaSCrcfuVW63nUm7lNDCWGZACcrIWZmjRofUEie4ZQKEAI/S9cIhVVUotmNiwm9Yz/Jzi4F3o83zG66XWD4EbtZ0BjDGw4uAI23lobCD+ICHHpgZG4dnpWRwIFTSXaBmJ9mP8Lj7bzLJGr0zJLE04gsmD68IGmVt2CuAPG7ifh0WEIUqP12oZjMcZzEpOE1oMcQD1QUVUfiTaStzBGCErjRoomzTvSKcCh2a1OshtD3pFGIeHD9A6q0MNAE9s7EhMDcyRJFBssnK0jTbkqLb6Z7gWVbEtlJ3SoA9DDu1KlIKIilrJeCPQt5hSEw2fjPvQ812mTQFIlhsZEigeiKId2OLMeD64XzrYorWqALw5f8jmkz4b10SwMN2qzWcZGpGsREZibHFoaW01ia7nLZNUJvQn/E6zIpHIYqwBAlIbcBitKAQVG4F7x3brP/leAbjTPTGYejbV8bqiiyHdid3aLQ2Z5WKeAY4yVusZR/tqmALxs8MExqCGDkPL9Oj4QY4buu9wUw5GiwGsLwsAGmjJe1uNIDb/EDGsMjz5igHAHO8jvm9VdKuDEDSWSh0YuABzrq/w3NDao8q1Ga3UhQp2kfdM6lhyra/SZqCWNbuPej7kgmZ8vud1DQBy3hQgYO0+vQsA1FzTweDLAIDluqyZaLnFJ9Y9xL3xspSBUwq3ittAhIYEZBKEBizOhjHWJQCY97QKAARjE/O+6ZDYbiEn8FYUGP1ZXC/WegxRdcWaANHJQkS7n9hb7GQpEDoWNtB+hcJIACMIM3y+oYlIjbRwQ+SfbFWDmUePra9lzDUA6MMb5g2gIBYFngAA3JfPcZ5GLNhbvtzqEPlyAKC1gTNAENIYuCfed4CxntdmasOswkUcrRuZtsisAQL9YoKPJiN9xkYHZgBdAMUOBgNJgsxP6Ar8t371V9Kdu3cYH6lxmjWcQnPS7t97AEDG45XawmsAEExvrFuys9fBwsn7h1pPh90hAQMDgGfL0KwNwCqD0gEATucbyluMULhbr9n6y8IdtYvlkOs8BPs2gEa5ckb+OpOEAw0SqJkr8f7+APMHQOZcxIyedBUPDsBkOkrj4UjadzBTw3f098F9igAAIABJREFUwjzA8yHOZWjd5O8TmBHgQiAuAEAzXvO+n5S3oaDJZ08TEbi2QsJmkD798Cdk9r145zle9+f3HsoV+OgSW4N//NGH6e7du+najesCcrpq5UXhk3FzJcaXv7e30jpzq6mBJeQBKGSi8MV1ZQmMkP4wkA2gnoUKrDgAgHRNjrW3WaeTY3U/oNAsGRR9HtxiWQCC8Qn+W0tLEd/P/XZp+9Z1Ojw8TO/+8H3mh/OF2OFgAD4NAMhcJu8NzWp+GgDQ+7s+JzrywsZ1H5N8S8Kj6DhjZwbMZsB0T8N0/fqN9Nv/4LeZhy7SfY7LqBcA3cqF5wBUY74NOnDCnqVu75BamdPZiuMDU0IAw7duSaNxMxWQDcYX4sKgK2CsEyYgmhurtARQSBBMIL7ng6Wu0OgkoAnxpEsAEDkWAEAV2GH2tkinp3DFnqZh7yoLrt2Ozgy9vva35fqY54n5dEBAG/NCGpqStrA53P17n2XtQU1gFWUdfg4H6pBBSzxyAcgBwHQImpqch6MDXt9iHesUEk5s2VUL7tLM8cijnK+VaRYBPmgQQnqARTeTMzYJGn2SMoBm+pwan/izBzOrbie9/vobzG37XTAfF6m7nqujCWoXLUmQAL4rANDu7tAYZUEDbsboSAtm8dG1KzyHoZDK5/kLAgC9lmrztJyO7vmfXwQA6ByPaz6Tap4MANyKERe0AJuoY6Dp6QHA31cLsBOksgWYCYhdY2r3VifaRcLti+eN+wBXa9xViG03qMRNy0w71WmAoHBrrExIqArKBFlP3AOQ3WX3AURx3dmF2BOmOkBk4GwvoLSbybD1ICqTkgYYCw2k+Pc60cuHl1wpb89sj1tZgd5HgS0Pmh5XaADpVVXgC5fl8hstXu2fxf6Q39I8xwhSGfjbXpFMFC9ascW/i0HU/o0LW4Crz9/nzli6Y8VMEhhCkk+7zVGJQhw2UIxCAjPUof3BbJO+9a/+Vfr+9z9KDx4+TOPukBvVuDPlBrPeCPgb9kdiscUAlnelg4fGD/L/BFvm0ru4NBym23dup6MDVeROz7F5CYQw20xufA212PfBP3MFNqjm1tihDs0iLdjeqio2rtfvL4cRz4B+JHCNPGtcUcm0ilZXm32gBUiAlbRcLl06zC1iSPzBKnNbLA9SXbC3ekz4rPeEA5c2NY0xNnL9OWSlHeLU0vOzcYbbi6XXxXEsgBWMF5I3jGdpTILEVoLWqASOiuDdlG3MRMGlYCNczs8llnxykj7//PO0ni8kwBvrCk8R14aEgOy6Iar6FCxUAtmTuQMOrCVYSSfbfi89fHBKULKXBGTigICEBX+y8n54GACoWhbBtMugD5hiFqWP1lkkQiVA5ufbHLQttgyACUBkl66BMPHAuI2C8YLPxeegwo4X2epFyz/nCEXGG2Db2ncEYlsu7gKIzcQs2Yhe7wYkzBD0fIRYP6NXaAZiHZfzCdeA6zQQyCpxGNHYWRp/4pyNFn7NT6zZop0zEncLuRv04fdkjTC1DyPZJFMktB2tr+TrwrViLrPVGK7Zsf44XgHI49mbyUfDiTjQsPWy0FTFejo/ORUAH2Y7TdtyaBBuORAXAAHGpoqPNZOp1CDic64Arjp8lzlA+9/acdt7lA9y20x+vb/cIXwA8udSC6baDzKgXJmBWMOHMaJoGxnGRogDQKM3E5qKACwMYFrb0wMWn+8Ou3JsOB+rPCePBeZntPboWqpR4lJofugDXNlSXIJPHp963D3aud1/q9U4igQwTwr2H2PFonlOWI/ef8DcxQsHc62nSGiHXl/BWKe8ofSk7LBO4A7FCMxFV7LrPJBFju1sAPHJz4Wfw/0L72szZcx4xz7DeRNujpMx1jKuV9fll8HE9Uqfz0sLMxB8fsPsiQMX5TR6SS2U2znf1ry3nmndAl+39JppXbRm+/mLIVrOhaYrIMe/YvqUrYIG9GRG5RyzMbzyvO54fCtg39+LAidB8WKe+tlrXCsGa9VB0h/103gyZl7gMcf42awKbtx8Xp0ztpK9/vUX03PPPceDJPb1TlILLdxB8b0oYCr/kiRKbyPdR82NDd2iES/Rkg1A4PjBSXr//fcKwLY9B3oDudji/bgmjBtygHfe/ykBhHnCPtxPq2jB8r6F92O/ni5RHEMHhFrTz2cLMuDQMk6pk67WF/ZRFN6GAwAC0DcDIID9A10ayFGUx02gUQen8Y4AuDG0twpGiKUdJCXQTZ2+GO+bnnIixFPkDTY9uHE0lokUW/MAJIKxG63GYAgOQ+IBe3XoAypPVD7RG0ijbHauToOHX5ywwHrzxm3eDxiGOMsMxyMCgB9/9mm6fu1aunnnVpiYRF4W65FrLDRwGUfQwkgJh0qiKdaF40Xz3DUPIF1AlnZvKUAmGElWzUQLL+fNIBiGNOtYJ7TMea7gfpan6tBJAzH77j+6nx7AyXX6kIBHLzTdPvvpJxH/DtL59JzEF8wvzwufK/bF4bwvRRy2ZmMTj9obQdYM8/LKbqftApuZyv6cZv/WPM/5QMSXvJ/EL5wt5Iy7SrP09/7e30u//rde53M7ujpkK/ZkZAZ7nHdCI5UAUreT5udRUEML/GqVZlPlHdeuXOI6uHIZGt/LtF7OCRxP+kOZH26SXGtHVxTPN9IMB/DqPEit8XLTVYGmMdzDehJhIEyrelqPKDDge86OzxLaiSeTS9Lr6yRqBV66LBfn0/Mv0tnpafrinoBJvHiOQx4ITcGR1t39+/eypBHXcTDbOi5cL1XIRf5OeZzZNL3//vtkquKFlmXOs9BQNAEGaTPXmXGSqgW13gt35VXah4P4AXYlktjumOvWhTwwAG/fvsV4yhbexSnjXOnK7fNsK78y3kFmIQBLrTsUzAjorhU3D69cTs8+eysNR710DhMYFiqV37OoUctHZCC/DYg197ddCMT4lduM8wA+s9AktAaxn2WZI/nZlv+W85dKiqMueHuf3JcH159dyl/o+pr9sdzLfS2NFv/u/Gc7H4/fNJO7wtf2vj/P8vb/dP7RVwAA1pOTB0O31H6FACAnQgox0tA6ayaGmEr/XwUAcxCqE+Nc0dsNND4pALgVUDLF98kBQFXs2xPoZwEAuUD2HZT2TNj6xz9vANAi1+Ucd8DkhsH2zU5KQ7QEDNLnx4v0L/7lv0jf+64qpQAAEXBHXThkQU9FiRQYdNT/iwr0PgBwOVNiNBqogvXs5Svp2rVr3JjJJIrvp95eaOKxJTJ+7kCXW+FiwvgAbsYS20WWy7QMwMBGCU4YthKbENOfT9V6wXsC8DUSUIVrOz050UYAPbgxNmcBXXjZ9Rc6Gw6MbBEI8W4DPj7A+tDJJD1YYdKgW5JBgBc1/JzcrmWUgiTDB1H8m4Xu1Raq5Mbvwb9jU5NWntYb7gmvhvVrsX8l6Ehc8FzOT87SvXufUysHAJIPzqCY430Qk8c9AABEwo+Nn60GKB2mRNHsy5cv8bPw/tlizoTowX1pvPVcye8BQFrwQAGGIIwsuI42ovj7AGydw3K9sA3VYs4Wi47W2iZBVYJOrTpWhsNFdwqjkzUrfRwPioKDAaQDWtaYDFdrXZMq1T74MX7nUr4BHldulMz4+Tu5wRrhuLFSKsK+nyd+Pg1DAPzMDEkmKhQOl36NXm4VVssL5qeAuHmAzjowQnT66PAo9QeN9qQNP3hN1QEeLQzUWEkbHhhoQII5ROaMgBIdqJoWSDv8UtMx7qd8TlgbBiANAGIM/VlssQtQcAXQEfdBjZdGH46AFv6+Baq0W3pdQPP3XwQANs+1vOK21uCuZLVOoJrfDnCi2lf8/loL0D/3c9hlYqIJGge/bIrSMGVLAHBA4FralOU6p6biYwBAH9BKM6XyvnP+U2uoBQBoFuCXAQC1jiKBLsxkyifyNACgnyl1jZbtBwEtMeUOLng1ACD3v6FbWBrNRTJ0Q0+VbCOMgdvcDcxWz3tfKfBJAEBfP2ULoqXGMSIXGvIBuA2ioaWMAIv3xYDEBQDKDIVzzezoLa2+GkKPgkdR+Gxia9uEhQfdaKlvtKgbkK4GANsrTn+rXYQbzeqI0wWblcBJzJuLAEDOMWo4RYFlBwDIqFpJWDYFe80XAC4AtCAB4rkAQAkgFTsa4gN6vXMevF95+XZ641d+JV27csj8AYwfzjPsqACqnbd2lXd0VmLy56JMrHf65fZ66ezkPL37zjs5dtr0Jo8lfVuU1/JgnJRXvf3uT7ifzjZi5q1gloPDnAu/USRFa9/HH3+cjh+dMiYPhqP0a9/4RuoPxYzqjdT6TLUIMI3TQK2JPeVzVyfQqOukxeyhmFRRKF2t0AYJnlkDtvOZhLmUCRvL0JSGNqmBcRfSuCeuYYCAsdPeAACwBLAH1y4xX5kMrZ0ZTESY2KDtby0mHLZt5nWnYmZCU42mA/0J8048XzCvHp4cs4X06OpVAkjQAuQ8iAHHPUk/LQrUK00grH+fUbAu8Py4ruOBZ4kFjEgU9HRhMmPA9ZER6XRircL0+fSY9ws3Yc2fdpGht7wsQHIQrt0bub32ewBpe+nhp5+m93/84/S9b/8VAV0wz7DfAljCvSyDGVoDgI7PWJ+OTxoCF17ajuaNeYfe9VUBgHX+n92R47oMAF6+Nkm/9Vu/lS5fEeDWG6H4O0jr5VRxwP/F/boDpp/AfF1RA1N5oJjXVy8dpTt376aDsZhpcKHFfBoTtBNQD8Ds8/s4F+G5BbC5kjmHDCeQ84SGJt2vtccgPz+fnXG94DyhXBRgL3Q01ZmCcwm+1xp56Ly5dOkSTd9Q2D4+/YwMwNl5L3fUqFMoMV6pFR8SAp+qawZ6pOxQUTxyQRFAs9jQYroCAPze975HiQrK7cR8XfU0rgYA1wEkllILOSYV/+M1cREA2LQCj3g+Wiy03u/evZNu3bqdBv0wp1qchqZiRaTJiZ/JLgbu1dJtqQAAztxDNn3muYPJhIQUED0Y754QAPQt5nWeuxcfDwB6HLw+dgGANfjXXn/tPLBs7dVnt3GWiwDA8rmUYB9D05Z+7/YT/msHAG0CAu0sARuVC/ATMADbia8HOAJdZAR+KFuVcYvc5rHZxwkLpokrcfH+xka7DQBak2vXouKEqCv6tTZdramww0xi32czzFcJrpH6phVL97nVw159KIMSJtIejYct22kwp7b0lhqn3C03uj0MwMZ/rnkebQAwGGrV46qvx/e376n+rAAgS/eeCxfoDei5Vy1qxe8ayKifi/XQ+NZa5B5ab0hIRmBnjdJH98/SP/tn/yx997s/4dsn/XEaDtASesqEF4kFKetOrIOxsG0H3gAX2HyQoGGDe+7mjXR06YgAICquGD9SyUO0HokSA3a07nht7mu9LgFAAhN0QlOyVIrVthkXMXeREHuubVQZspD1OY0nzpnsoWX36EhurNCSI8uOTLIq2DrWBJDPA1i8x6w/sAlgsuL7wvXKTERGIEgywCBU66nAO2zQ0I1RK+uSiQe+P3/GUmDhpUtHfL9cc6MFJjQGuXmzxU3xzW6QaCWhQ9n0jJoiMP8Ag2sAmd3CZdiaiwBIkVjhRMDW0LE0/8YHYwKASMapxRVtzEiAmMj3xNybzc/1uTEPRyNVog164TpZoVtJm2i1hBixmExq2YvWWrtlWUS4lXQIuBNYKrYaXNE4lmT9aX4xphcMPAW0oj2BrB4BUTKpaNw8fQBAAlBWfMEYMTCLn2fjkszoirgTax0i0lyWqMIGSI55MJtKw4VtJMV18TnG+OY24Lh3zHckfnAvhHaVQWq1litu4No8JwlO4uAI7ck4hJ7PZ5yP1K8KFi41IQE0B7CM92O94j1g9tIlOp+kA1gN4DAn8C15h9J0BAD2Mi1mYq9ap+oiALDeh4rHr+ec9699kTuGtbouf04L9OAPI2ZcoAVYFvT0XINhUFxPmVj1trT32jGl1CzkAThaX3x9vVyRbhifuoeGUczpE/mBD6I5cY8NL8fZCg/CQaBOSCXHYCAtvi3j4BUTJCeR1X0ZAHQFO/Y1XHX5fTy8sSAbT2ZPXqPYDJaM4lI2QMrvlwuoQWg/Z5s/MXUM11ozePVes/YN+GP9NclRzRCo8yYzkw2gNwzAfAXV1G0zGFd2j95jBjaKg1nW8skagHY/bDRXOSsqE5X62SK/ag4pbeacwlA8D7eiPwYA5DosTKnKGy0BFa6TrdynAQB3MQCbuKL4y2vb0d6fgfYKOPS1+HMaoKEdL8AtkvuqCl8eP5vFDKOFtd/DIXuRDg466Y3X30ivv/51FfBmcAQVm1zfEQfUcJ3urDRffe2Y68x9AtCbnc3Sm2+9JdkIFgjlZumX8y7FBRRx1Znx5g9/3AIAwTTPex6ZsJAPGaTj6YIAI7QAsU9A6/c3f/M30+ggGHldGQzgugEkYv4i3o9HRwTKXrp1RQUIFI0ApHQE/CFPws8p0UFzqiWZ+JDhgNYezBFwACcTZ7FM8+jIMMjbpZTBJg06C37fIAAxmIt4XWIMzjcABBCLcG0YZ40MgA7sh8gzJZGwZp6BDgLMyYPRAffbs9CAOzya8Pndf/iAefD1Z64zl4EpCvO4CF8NEcHxIHIClNCgrRfyJnW8MtMNJgTlCyVB57uK74pjnU24OyPvQD4J5mjMk/L5j0dXpIEHgL+LwpqYUn26Am/SpNejptu/+dd/wny72xlzflw6uiZTusgxa4CmXp/N/uCBiLiYNSjb7r15/6REQSkcVq2v6kC3fe6MfC8Y0DXjbNYBc/MkPXvrRvr1X/9mOp3d13xMM+p7DwfqohnQRRlleRVpwcjF/DicqGDtcej30ZmEnFRGhc/evMZ5M+4navDNTk6y/AuZeb3LfJwOXxvYJnHuKt8+PxMAjXVLo5yB3IDRkQLJHTD4OT6Yo8EsNkiIboyzM8itqFiAc8j4QC7kna7y2y8++4LPEdI0yu0AkqPtH/cAAFy62WSZkr0ahIdwBR4y3qwTtAvtbv7t73yH5zv83sks5IN6Yh42DMAokFR4RrWZ5b/uYsdr3JpzovIUEEsgbSFdRbCqn3/+uQQJUp4R5ufhVr8IYLcpbGietbvdIDHA80bkp1n6JMEcbQmGRLpz5zalCbCOcDUECKPFeR8D0DfWAIClbnxTaKk1+koPCn3G7ryImsnFvlAy+8o9C0CuP4fPt3oATa5Z4iDNeiz3+l3P7iIQ8BcBAJZ40JZkDgBA24tzQoUbMCijHN4nAAB94xzAzCxza1a7heEXBQCiouQ2W1+f9enoalUBgE0rrN69RZz4awQAd010P8hahHKXKcqu1t8mssQCyvcXBx8fgApGj+ZDHk3+z34GYIzjntbmPGd+RgYgAv8uxH9vIN0DANbjUQKT+wBAJp3BHOmNBQz85LNH6Q//jz9M7/zoHhMgiEoDAEyrY22EI5gyLOjKhuuGGQDb/4r5hYOWgRTok7HS1RtQ++XOjatsVQSRDgkhAr3AH5lveP05Yc6aEjsCJZlElfmH3X/L8cP1bQdevcMtYTKrUBsj108chFiFo7kBbOpxryteN5MMinVDd0N6PSUgaBMQu5ryM0M/kC6vNr6gYK/aa1lpDOdXAzAAcbCRizWnw5wqh2hNVtsBfh+OsAAACe7RLMOmDhL+liA+dPjUHi0g8TzNzs8I0C/A/EL7Z2jWQGMML+ydbBVeSlusPxryuw0ADkK0lyLpFNoVQGxXMrRAqC1aGoGnZycyk1ghEVlnl8W6LQ0i1/is+UwadNBSUuIfcTn0Aa3hWiewZkRlMMsi3UjOot2a91c1kZox4o1vNoNGJBxsZBaQjRMiQUA8adpBwZAUAwEvuiH6oAxVssgSzQTknOiL/WGwGCxcH7SQ+PGeyVQUk4cGKWhVhmPjYplOT0/S6elpnhNwacQ1oqKJ9eZ56fnKGBhgH+cz2qRCsxCfDwatgUVpDjUumvp/MV90qF2l+XSWHY61otrA0662xUZXRG1beC2m53SjxjzXOAvorBmAToSc+NR7ZI7uGRhrIsG+JDTH8se2B+8v7JVz73EAION8zeAoXLR1HRVT3q1kcZF2a83zCsxcgmwle1KJcPnKjL7M7Aqg+wkAQMeqEhRVC6C/V8ymXVpwTQX6yQDATdk+HGATZ9UeAJBAfCTK0jYyQy50R6NgoIS6cYXP86Srg8Y+ALAZwxjPqtXpQgAwYo3nbQ0AbgOsFbPBDKTCbEkArBmUKhI15geh/Wex/LpIZROVurXJAFmMM5513TrL73lKADCDdxWbwMyico6286AnBwA5Py4AAWvmYA3w5+8uEAheeyDsaoMdoDrFSzZje8gCJlr9pL+7Wp2kmzdukEX36iuvUCKhbP1rWtekz1oCgLgHAHNsXaUecT8tpsv05ps/yFq0YN4hV/ILphEA8tah8zseyxTg+2+9y/0BDEAU9FYEsptYDldQ/N75cp1+8IM30wrAeZin/MZv/GYaTiTib7fs5UpmCmfnKqQNepN0/cb19MsvPMO/g9mnIpauLZtEhVahCgZNDABQwvkULXBrtkjj3lcsfE1hsDadppMHn/F9/TW00WD2oD1Jbe+rtBwI4IBZluKUgB4UVpjv9CD3AlMguKpCsgJGbjL1IbMwxsv5AjpPMC69oeRHrly/FpIHASQN1BEDgIjdGuzQwJ4bDsQBajjuLeO6sllJmJZ4/iGeyxTIhaI2ADifqeMGu2S9f2m8kJciSXPxGnIgfTKmOG6rVfr+97+f/uzf/znzVgCAyN/BgMT82AUA1kACY0FcXz7/hgZqacLD55klKWIDLkXCNVHKJZ/Psf7h9vkv5kxmMAcgaLfy4RFbYa9dv0wgpzuMlvWhvn801Lz2svbngxnOzoeFiu+dMBu0+QQBtX4v3bh2mS39N64epVu3brGlGuuWOTBbY+Va63CKgp+Ywo7PAcoRgFQBwVI3WMedGfJ3tZBbAqns9CGzMuYH1ydawNfoyJly/o8HY64XaFTjfTZdwzgbEMNzdmcQ7oudWwaOIM8DmRbk/jAJnJ6lt99+mwxEzJfUD+Zzt80AXHWfDgD08901h/VvJkg1ACDiLZyP0YqNc2cNAKog32holgznPJ/QAoz8Ohf65Uy8gnkWxn84SM8++yzZlQSCCfY3+VQ5H9kRU3cQRofUVoHYGs/x7w3Q1nR2lPddYwAuHDdFrQZIL9/bMADbTOXWIisK0fk6ysL3Xl3si1mAP28AcJ8cXD5L/Q+/FxqAuTVLiDVcmfRqi0M2A6MJ17VocpH4MJDlkk9DedbA1xTLRkOMgSCQt7qS0QROJWhO2Hzg1HU2LcAOlF4wNdsozuf5draQ9H3nlSoAX9RznT+3css1GFnz0fbzLdoASQ4IFeOgXmD7Wn/rCd5sIHEFFSC47z7NoPDvN9cfAak6MNXf+7MyAL+KFuD2jtq+brUONq1F5XvLgDk6GjPgv/PTz9Mf/uEfpk8+kSjtwWAiHY75gzQZT9LhAcAgJF1KJlFhUoIQB38abjRtSvhuACiXJ2OKK189mtBkwrbuM2uQQb8PCUm3zw3UjKWmBUFX7vlYtgQLgKz+vUDAywSmWW/NE3drjxNkHbAE4pyeo1KNZFXaa2yDDsCO1xPMY7bvsgIXFV66+TUMwNOzU1byAO6gsqhEQ0AHE5Q+2neDVcWEFuYOoq8DPMOL4tsDbWjYpAgSLtRugOuiSHDBUrMjrNqKzapEdXBA8xJe03yqVgNU+KHFsxQAROdmjAP04KjFp9YXHEDYIhysr97BgQ4W0Jxbh9NxsNm40UaFczgYUfNvvoDGIFrJFTdR1cQrtzPP4JAHow9X+sHmRPtEo2XF+JyZmw3DpdRA01zRGCFBxDjapIXXG8wVVPhLlohAxuaAjUMI25GD0WAGpT/fiZTFuT3XOCdxoKOzM9il0qQUkFkchOK5+Pfw3QaVy40e89GHULfV4uAznc7To0ePWFHG51pjDy6EqIDDxMbmINKkbLTiCLDFgba8Z7cfW2MQ88hz2UBi1tAJkHCfdgjBywJsMNhZxiG2qK3mZCjMzqdiu/TjQIpiQ/Gy62o+kOzTyI3fMe7Q1mDZv0vVCVJz7bt/Z8tkJCrQOR2JjacBbBwnt1svccn1PpXnReQpHs8MYEQBCd9XFkK9FnIsLvaxErB0HmJWW11pbt7bzFl8pltUy4JLTsrARLHGUH729b6k9Znd/PL7mwIM/71uQ6kYgAYAMXY0xQgX1SYfqDQJo6Xe4+J573UDgg7/ze3vLUAI8VCgfj7AWyrG129XVzMbc6KtBL7O27YA3wq4hSkVf68o/JUxqjEhiTw1SyO0gUDr65FhWaxJMNC539XrNB8gI9bCPC+YmH7ObgHm2AeQned5UezIz8J6lJUmX51TlXl2DdRxjZTxpGLkNuu3yUccq3cD1I1GmJ97vt7Yn8mw6khOg/qq3iNQkBn0yCTD8pKZg8wZ7ty6kV5/44308ot3+XGLJdjsS7Zy8ntCLgTgDfZsgq0ArCi3oPI+NdoW6/SD739f7bnUAd4PAOLeEfNRRAEACAbRfKPWWDMAfW8AAHEvaAH9zl9+J01pvtZNvcE4/Z2/+3fTcCxW/mI9ldYv23573G/w8+FgnK5dv55ee/46P3KzOpf7NlwSkE0wZwJrXIBIF1rSHWgnqlhKcV3OG3QLQLol7rsLI6tBGrGDA+ehc2nxLmdh7qH9QM6qq3QPYCH+O5erPABFF6dUHA3Asp+YBxCQhWYxGWDdtF5qLSP/4vzGXk/AQPchreAm9gFIdI5VzltrMo/BvocZGjSQQ8/RjGkV49oMwFFP2ovr0BCEuQLncAEg6KwV45r3O11TvyMgA3kWciaYzeF7xlFY6y0X6dvf/k764Q/fIfNytVS+hOZsaWQ3d0HgL76/ya8Uf2yyZzMLfgaFk+1CHfG9YPQxz4p9K39Ldf4ElaV85X3acT67zobUhYEiskzX6Xy1SVevXk1HVw/S4cFhmhxqPcLUEM9c3nOSAAAgAElEQVR/OAjgOwrivbV0Sct9ls+djDC0Ao/UabMW87TfXbENtbdZsqB6aTJhqz9/g4XTI65TdLIgz0Ihnt05vm7UjsuWb5/z49ywWp5GfgRjOTHfmvOIxpfad2DyFXuP9yB07Jhl7vwO/wagj51FXUi7qJ3YUjA6C0QeOBcj8OTkOH340w/TLEwRnWdBmpJAc1eMYHi2MM7F+kBLuV+7GNithxt/KVvltTZQQDQAiPUJYzOQBvrp0qWD9Pzzz6dxaDku52eKU50wuLO3AObtDr06Ex/w+XzOKxAiUB6Q9inOLzdu3EiXLx8p1wjtQ5/rajwC41Lms7BBwcvxvwa4nS/zefH62nmUzQGbcWoX0Osx3S4Yau+46JULqNbidJ7yJcC/8vsaQtzPTwOwBAHrfKCzCwDEYPjBZC2erdLC4wFAI/iliHWZmORJ330yADAzTaJFYxcAiM/M7nVu/YgvuggA5CTkIgpq9p6W23qC/iIBwJ3BYAcAWB+Qy9+7wHW7YYz8/xgA1IbWUIN3AYAOjAh02MhGlyYUD/6L77+TvvWtf5UePRTb6XAYAM/yUTqYHKSDiRxQEYjcylkyDPOBM0BBvA8A1LVLR6Ryj3uqwCxmp0qwIvCzDYRGGeG2FlUvHFCUbDSzgG1ZBuyDam9AMK/LOKQ5APvnAADbB4iGpQWwxg6JADhRoYNJiVtAkWgJZAt3RoIncGNsKPbWkgMD0K2hNNkA2xGCQLCev3KV94N/B8MM1zM+OuSfaGeAeDiYkzdv3mAyzwMgDiGRqLIVidW+JbVm8O+4Jr/UntyRqcN8LlMSM0bCpAStm2AATs9P0xSgSy8xcYVrHRJlgZ6IRwLe8Bgk/j1gOxQSXlbKDw+YOMHEAdeB5AsAJwxOkHA8fHQShwcAUhJMx2u9mXFeUKsvDqGcD3O1vuKVjU4CACwPaHme2Q0xWAHlBoHxRBIEnUZcC67NyUevowTALeMlq0ax1ECg2HZIqNR6I9dIJsX4e7RSqOVYILFNdwiUUBMSTAocCCTC7znpdZMPRTE2cnED4Kfx0fX8v9S9WbNk13kltnPOO9UMVBVADAQHUBSHaKtDT3LrwQ/2z2urRalt/wdL8kN3KGyFoiPcUkRbsluiGBxAEgRATMRQw715c850rLW+tc8+OzPvLQBkU8oIoKruzTx5zh6/vb71raU+9c8VAMrMBn0MZiVd4YKxin0E70fpOoJSlJ7zQEeWSgiq46BAALdhIJTz14ElS33JMFFbUHcQmkVgqCzDtdCaioVBBTVwKgBH605z6uDBFgeR7VrjkQEoAG25NadNJa6+brQI2QefAwDcuw9VCYPd97QDs7zOFNqIMbLVX/GG2mQAGjNXva4CAN2W5ZqWKxzispBC0fwKk6SY91kztWaEGRis3N527/EwAKjxKXMQ3+MuXPpsAKATtjk+yi6qcUfPAACWWpJu7p3SkWAD6lBeaMtdAQDiDtDe+fAFgP8AAJgZFrkhFZftSllUjM8CACQwkbWWmnlABnGsT82BZBcA5FdXIne1xImB2hoENKM/A2rBEKtdmP2cZSVH2Z513JrX/5xwb0ZKZgvuSbTX47EEIevffRYQ0Af1ctyVBzx0L9Y+PB9LX3HABws7WKfoH+x3mHZYJ3Fgx/p48+wofe1rX0vf+dY3uPYul5BVWJCJhheY1/4e7BE2AQMASCYQfB2GYPZt049/9KMwpMJeIi1iPyOAYQJqIWvBsuPVOv34Z2+x3HbVERi1FwAcDNNy201vvvlmmtEFfpuWq0761re/xVJDlswOtvw873mzZSkv9rhBf8Tn/p1Xn5c23XqqUttulAzGeoKErg6/ldgiHZVVcUWgrz/S+WWrBCvEE/H745ESj71VVGKEeyvjH1SMHKk00Vpp9fegIowaaPNJ+uDDD9Pk6TmlTk7G0C5UKShLOSNezmZs/cb8i+tp7Fv7tD59L5xuADiLNYt7fgBAYsPXlSICgAAky216yKQo/s04DokdAB6REGsAhWD8LGMv74eMCkBYaNX1ZSbzzk9/SgD500+fyORhoXaHGzBLSStA0omGDACSgYYEhCesgb7Yx68AANkv1zAAPwsAyG525Vvsb4OTs/Stb30rPfzS/XR0jDLuCePTdYzHTz+GC+46dQIA7JsRjv5sWdUOdK4J9/TZEjFxL908G9Ld+4SVHb10Oh5L53q1kNvwQnFZmRhmvB6Jb8R9vO+Q0kBCB+9XqfY6nZ7IDAbnBkkYtVczMFg5bjqKLzFWLy+nUUY/KyRYmn5hfJqCTMASYDkbt8GT+KKlXL2x/sFs6PJCJhvTyYznlU4A9wAAed9evwLA3G78fG1Zl6tiHCfH/R7MkQwkhWbiGlrD3R5N7XBuRCk3XgIAoQUuSZ6msqtizpfJJgCnca602R+IOzzPHB+nW7dupps3b7B/cf7Aq9cL5mhdIVTFHxkAdFhXAdzPAgC6HbQfHE5O630VAa3e0ONijsM8xksAsKnAkdTEvteh0t8abPxNA4D7wL/yfjMAmAPhbhzorQUYEWD3AABYatM1i95hBuCO5l0AgA3FWB1YijO647jhhajmIQAwd8iBkt2dUpjCZvmZAMCqt3vXHqAisKze93kYgPsG2o6m3zVg9uH3fzbmnxlA9YGsDtAbDcA6UI+44wuWAH9RBiAOmBq3ur98gDcToDgMlu1vxhJ+ho2sO+qmDz/8Vfrrv/379I//+P00nwswOB2d0BxgkC6p3dfrKABE4MDMlDWDMlChAMNBKjKuKPl97tYdMsCwYPJ+tzMGfBCnBgACIAEBigMwaLmzxDcopirJhdvW/n4o18HygOZn9rxisFkwT6ClIUBCgZkDDGoSckNuAEKAlGgrA1MKSLDRgrUmkASJAwIfETA6K4j38j1w+gsNNbQrzA8MmuE9E2jjLAU0wp4eQS4yt7gXBPRgACLTScFtMMtCpL48EJmdI1MNsQMFMiGjGQFIZAinEwGACDDxndgUaQTB8uIlS6TJAI3nsostGAAIuLa9AccDDjhodxwKnnvuORmLTKcszeEhG26A3IgTAwsy6rodgo3OTBLkXIpthiAY7cxsq10gqdHVHNgVSInBgv6TBliU0kBPMYSaqW24gdNZAwDimsyMbsE469L12Ifqch6NjxT44L0cnxuwQBU8KMMa5WBRYuWxZ602HBzZT8H8w/3nDChdhg0QxnOxnAj6TE1mtZy3OFABsGeQSXaoAHbcH5iA5xfnGaDD5ol+AFA7hntzfE7AgrT/jglGB2gUY533W7hOU0w+zEGcTfZ8ygYjpWFIwf5ye3CuZJDJbAYEymKe4mBlfUuCmBy3GN+6t8wUyxoBWvcgU90KCKpN5jod4xJMK8HEdrCMi17NANwF7rRwlXEHx+mBfX030xz7SwEwem1ncOYMbqz/uF8CL5l51ubm19Il5T5aatqVB7/9oaHGackA1HzRutqU/LY/7QNkCcTpeXxw8f222xlB+VUvPy/GOtZTLOWtoDE7yFZMk3CoHRmR8ZdgLBbPUYJBHMMF+49tXkjNlPGj4gvtK61X7F9uDxYrui8BihTjQwkk17K3GW35mmAksCzXCaBwlMU62dJebhh8Dai3zQe6Zmw17VTODZp1FKYa+fvj3n8dAKDHkf4Mc4UKwK7Hgsf9vp+Ltdz+jZ/TjMhtBYDk/i4WDvW5GCdmUzs5M11MgtE/VoIuSb8XJiAoXXvttefJMIF2Ml11XRJOUy2J75cVCE7oGQBczdfpjTd+EklIgGXqZ9wn2dQuYYnrInGI109+/g4TX3ABphkCgcOmb+EGiv0QJcLn5xfZBOvR06fphYcvZKmP3lCJuMHQ5gfSTBuPj5nQe+neUTo9OeHz0QU1hStpSMtw5aQUBmI5af86PuR8WokBqX0dFQNBzKBLKxTbIhG5FfPQmp00rULiMRLMMMNRPCBAwozW9VolnsfjocCTi/P00Ucfpfv37hMomjyFBAoAyB4Trx9+9Anb+tad29R8m01h0iEQEfs/9kGbKjDuWWg/N/u+XK/wnPX+5XOq++I4BcMdutUoxYzlwlqTWA/oggxWWySom0R1N50NpkowgoUa0jtsp6jG+If/9+/Thx9+kDCOsL/N5gBUN3R3xX33Atyt1znPmkZjO7Q2o39gL8RXnaDbkbBo70Mutc4VbrW0jzWYK5d0lxoCsOd8iXPHfLNJX/3q19Lv//6/Ivt1vjgngLVaC8CDizrBr5XYnF08O8bpWlIw0MDmPt0fCSjrQMN6nWaIryF9s56l27dupa9/5SXFUsCmUaFzecn1oN85UTtUhBOfJ8xIZ8zFOFBvRwUUGX1dJbvTViYkdAsOxjHnw/qS8fFyiXMB7lXMWJuJdhLM7cQexv2vAzgnMaHQCs1rfqW3Ouh3eT7DuP7lL3+Znj56zPG0Xkj7ehOmpVssBGyoYOj3Yt3agEUaP4u9sVxn6nWZjxrVMWbE2RyJ6yFcgPm9IqDA0O7FF19I/aHG23op+SWsC7jPhgHfjhvyrktgdd0wjkMrlddCO49GNCKE8zelEpZav6xpDaJA+Wq+Tz8FM7Td//ueOFHqgdOlqAgo23P/pw5r4+Y23jkPtxmKzb7c4CMm//D+g3Fefv8+8G+XiRjEnNiPpL24J+17CM95RhdgkxAOts8ffe9/24rS6wd0hwQ18xkBwFbwhkZxZteaMkaUqwGxeUYA0KU1AADV8VELnsXhrQERj/ovBABs3ALjUHeop+qNId732wIAfYD6bQOANgE5tAHXzVmbgHxRABDj/uTkJE1XMwZG//t/+Kv0/vvvpeXihBmRs9EJmWTj3pzvA3Cn8tShNpzQWjGQ6UMDNlGWbqROunP7TnrpwUMusP3uhqUpoNZTg6yHbOeAm5sAI2kKYsPHAp8PiAXjr1xIfQDIjEAzwSrtLJcsZsZHlIAiY2+AiwDEXGLWCCIAzFGzI4ARBJwAvfBzlKkA+BsOURajwFCbfMzv+H4z5XDPT548pqYeS4GDsYdM7/EJmJVH0l5bLnltvAx8AQDEdZD5G4yGBGIvzi+4EcJduVx4zX4haIXAAaxKuBUOVXIshsE6LZYB8s3k+ooDkbQG5UpnQJZuhNSdiwNZtBsAQNz3ImsFKpDCGIE2EL4LwBsYnQjM4LKGduoNurmkGu8HMGmQE/2PkgaKGA8g1g2n4KM2Y5OHpoYB07B92lR4fFaaRdN0/hTlscioy6DEATvGHw5wCuRUsmMQ0G2PElrcI+YAXB2hhYTfoZ3QL6OR+gpAaTtQ0L8AiBAwCVbANg7q9WZprUuzS61FY/aFf9+YlCgjTE1HiEv35P74+PEjHvww3hGwYkzjNaT2o8qeyHwYSAR+fCYRa94rArMAJX3QxQasUuMZ2xLjCf1ljUuzWnlIiejW90rtyNBBLAOIHAAyISbgCjgMDibWH9ys1M6jgU9EEWQWACDueVkcNDlnqgXzWQBAPXubTfhFAcCshRjAYQa+nhEAzOLOLnEpAvdDACCeoxSlLsGRfQAggL/GdbXthLxbmuqG/fUBgFrjbOqzHwDUeN1fLo3PZyAaexG0jYqAmCzIHK+VTLNw5MaaFww7XsslrkIZ+MBi7Xh+tA86mjRxgGwxSOXqqdfVACAkaMrYE4COASGNS7N1Awiv5AP8vGCGtQDVAACdQOuGaQvWo/L7aoJO4zrctDnnRoAqpYTGvhJg9WnDqNS/d5N2OsDvHhgaFmAbAMR1aj2gOmYvp75BpizOX8hx8P6sKZbHn9s3nrseczFv8/oMtghYfH0BWjC74F4VJh1grAAYe/31L7GE7fS4z/WTAQATGl0mcpBQOgQAAqBazpbppz/9aT7ks4IhZD4MAJIx6HaPhNLP334vPXr0OM23YuvXACDWW8YUK1QKYM+XNhkAQMRrBliGR/o84hzJd4DNuEqj4RFjileeP0137yC5O9a46gioc6k89hHOw6zR4oqO9nnHibHVpsu4MjOoN9BF7qZ+CvOCWE9ROkygwOM+KqsAWJQAN9RTAPwMoeG8WiUwwt555+300osvE1xAP9BkgUnOTZoh1oWe8q2b6h8noM34t8ZfsDBXy8aUDOsPkqmQVoGcBfbKbWhkm3AAc7NSBmO4vBSblMZ1PTwl7wNahJz/YH5xb2rMbqQ/KeOtweaJ9m0kIBlzqmIDjDfs2U+fPGKSFZpueBkA7A+OqLnbiT27iXvac9ca2400REgKOeG5llRL86oBkybZp/e0S5ydcPfnbYrpnxvcwvPzbFEAgPhMbyRG3v/4P/0PfO7RWGXccIsu10+bFgIY5byNxBpS/jyrxLm80z8WEwzl9ONxWiylTfy1L3+J39Nfb0UAQHwJU5y1gGecw6hZTabvPC2o7QdguIkLtSaJAOHE0WJ9oSqYtXWjQX5d5rhovYWpDtiuIEdAb1IVOAAAeU5aXfA8ZbM5SPuoFF9jBEkxVzWhPVDxUsYFMO85OT1hzPjo0aP0+JNH0pMOV2QD6phnHo/8swAA+e8KBFTb70d/SgDQ4xXnNwJ6vSMldunbIQAQJdj9YACuVwC8m8QA4naup0UGlvdSaG8fAgChhbkeDliRhTMLCRALAbIA2Xmeq1zMawAQrue6gYbRXe5D/jtLqqONtI/FPLmmBHMXF2iYnuqQem9tAECeAfMe28yHsm+sYVwmIfNcrKp5/PNy3zaz8jcFAFoKrLyn8u+dP/ne/1qldh1UNICgGrFBQFuNupG4ZT1Y/Z6skWMAoo5FTYGOuwIQwglWacHkQVEdFJ0xx4JUdig2DC54NeDoGu54OzQNvNBxHNYpz/xsmbu7N6DeYSBkLTc9D5bI8nVIlHvf4NfPDjDoDvy8cfH1Fa+hxlozwwnza5iNbVfYolyqyvzmQV9lipvP73+uLAp64OyS3dt2IvA2k7Huz3qc7jO3KD+DBcCgXNk3zgJs+3K03XTPGGj+H//nf0rvv/deOjm+x43g5pECuzNueChJnEXAIsaFNxmUYGDBxoGG7DmCSt2UNguK6D5370wHgdAM9D0yiGPppNx/sfA7A8SFpiPHUS/KaE5sdrgnAA4blMNGlAawAkGXwDhlVAH0YiNHpgv3A80UvOhAh3nal3bMaqlAC5lUZ3/IYowDoMERgm5RBowNBCUmaEsGaAg8Ks2TPkpgQt9H4MacASCCCBwYoF2CAwDLdZcLlZCEIQiub408Ay/P3X8Q812adrNLlVLLsRVBrAJ5BCdkqEXZKIAilvAsF8HAE3NuNZFrH07S+HOGQAPlnriOrMwioI+DY2TUukNpzAEoZeZ6rVJeaP3hZwigcD0zJn3gw8HLJU/cYAlUDQkWog0kJD5Mt27dkcZhLgGPQLLSpnLgqswo2DTKbAPsErAowFogr56FDL61zCbQLhg/y6XKd+0iKqBYjA/1vcxNVtA6oq7lkoAsAkqY5OCZFazCCTGcMhFoWmMvDpw7JYD8ebOG2EQFhyuW7kJPqK/nEtvOQu4IxBH4ryRKHgEayqcvzs/T7OJSQLMTYCypVek6M9nhvDw6PeZ4R/8QQEEoDBfoAN7yPrhROTUYEzjgGCgkoBqlvgAi972yMHX8sixPVVCscSDQWmzD2WxCliVc+va+KjOX1t5UrP1gd5bMPjPX83PtCVKvLiuuGGpxcKjBjN1Ssf1gBw48+4AQr/PXSnSE6dkhk6N9QXgrAI2Ga97Xvs8dkCVK2gyMmfnn9q9LgCnSXWgeG8QxIGoRb65hDtqtH9fSpTsQYIf2D/cQjkWz9Jxg1Z25FLvFQCQjI+QRDPjFgThX9ETJVh5fkdjJTGO6KhZtVpcA+UI5UPdc94GgCuibL9I6n0UszXRuxwc4ALjyw+83I1rPHaZJOc4qAM1Ci690/819WcSSOU6t4suD47MC0GpzrvpgXDJsPTdLcDrHYdmoxwPXlQ9mQroE3lqNFUM4mN6WfPBz7QMTxaz0+qHrmH0l4wdo2SlRiXWemmo4ZBflduNBP/3e7/1e+tbrr2hfXDzldaBtJ6ALDPdwbI6ybgNg2Fem03n6+c9/TpAO+wGLJ/C/iBvNGAWgh3Xz9OyM6+a7H32SPvzwQ2oAIhaQv2UDxqLfyEga9in9cjldpeOTk3R+OWeSc3x0yhipP9I+gwoBjPlRJJuwDyE26WxR1QBNvZ5cSnsyRRv2pFl48xiatr10POzw/o9HAK4QBwp4WR9HXBgJqMxYiX3Rc4uynGhvmIzRME7zbtuRhAXHfiE5IaMFJFrBnJO+Mu7n448/Sb/61Yfp7u076d69e2n6FK6uy3R64xbb7Z0P3uOe+/DFl9g+85BtwUGZpigBTZJtx/Zsj7PSLZj301ESzDHrZqkYDMxCGpRNngQjLwBNuiHjtBVAFSptQruX62iMN0s9HG0h17IksxRjGhU3iE/ABEPMOJ0t0iWcly9n/F4DXWgfJufy/tWWGGCsTPa1ytMR3zFxmuPbiD+K8yz38Uy4acc1jaSAnhPay9hnUTLOPzdKYqKEW/G8k7GSHSEhAOeL3jgzRtH+i9Qjw/YP/s2/pkZfvzeT2UlPFSYYZ6r42X9Og8Y5x45Li82wjXUfj4O5d+fWGU32boyEFxxBjxvzZysAahPripf7dQexGrS6Q0s7Z6qSYvAZjDlW6fypTF5Y4ot2Ds1LxJt6qewXpfd4ZSAT6w0riaRRjfOW5HTCuA1jCP1F8yhUWsS6mKUXfOaFkWGP56J33303PXr8lHEjKmIwr+EKrvYPIDP2k8Z1fjdB5rWa7VpqQhYVIN7rVSm1JZBP87yofIFbNtbF4dEwvfLyy6if4rycnT8JR3bNqU43JAAYP0g6QetreRftv3OcQe4Ac/torAq1556TJupixjgZn+cZs0gAluPEV8wMwMNf1+o3tYkYm/z7wdymGf9+Q1tb3193oAKYTHQNn0avWHtctR/GhZrmiopPFKsUAODO57InhPvf57OrG6KucMV5qXwhUamGifvI+327vRwjXAsA2hW4BAA53xzIfE4A0AFDc33P19Bs+oIAYLaP3gMAlgG1AcB9AdTeiRglI3U37QMAYwRxYtUD9bcNAOb2z/ZLGoB1KfYVy0D+lQ+hHBOfEQA0YLD7Pc7g7b8Di+02/ZentP4SB4lD4F1z1UMApN5RuyxzbpWLQn9O9tmTy5T+y//zX9J//vt/Sp9+8knq9+SKdBIlWDfD/RWUdAIpg45EmQ0wbwWAYH1RAKsD2ysvPWTp79EY7sHrtKX7awO4EjiKEhEDgFwgo1SSG38IxRKIsUttlClOAUJCg4YZWjHXnHnD94/JkNvye/FyiQ3+TnCtJ000ApCFW1S+R+hMZNc5PRM0fRBUCrByibD6oSzpYmAamUYbKZCNHqVECIjBIMD3IihAvyAgx/f5/QBqbF6B5zo+vREZwQg8Axy1MYnxDBxKqEmUEwBivvm5lJXfplvjYwZKq80qTS4v0+Onn6q0lHpv+pPXiHUa948AtTNQaTf+ywAgteXkWuz1ARk8Po/dpgOI5BgKkxGAXSxDomSVmBFHR6fMYOv+S3fmhkFgjUWCexsEiAggNe6pT0cweMm2NADI39EFTAcCazxBrFwAWFOK6++WhooYaT30F7PIyvACscb94lkQ0CHDy/fZHaxw3UQ7u4Q5z9/8e5dm67owAjFYJ8HzAMqiBFharzgQaBwgE6qAPbFU/Oknn6aLyUUWnZemnvoGAOsRnLiHg7TFYQ1zBPdG1q7AUrQLnqnRJ4Rm4irNJ3OWGVNw3SUfBQAoELpKFsXcLJkPDoCQRGCfxOEX7awyK4C/a5ZulYfz3G4OHAtHZ/zuek1AH9wUWO29dnGdGkCsA6E6I+z7uwoALAHfQwCg7+06ALAWYc/tE3+pn2+nBGXnfZ8dAOS8LUA79kNc94sCgLiME7j7kpssKSSYcBgALBPANQCYGX5FCbsWIo0TSC6Ur8zksdRAMADymP8MACD7uIjg230V69wXBAAJEMa6o7ZsM1LywXDHjVpPnRN1cQ4p+wB/f1YA0Im05pr1wbEG6sTSK8vTy/macftcQfObAQCbvgfAoiQikp7+uyUeuoM+45ASKOVY265ZKvvt119JX//66+l4tFHiNOngXgKAbhsCB2BtPwMACJdijH8AL4hjwDoEU+/9Tx4TALxcQUy/R6kVvi8AbvQbmERgBEE6Ama10Hg+nwloPDpB+essbXtKaPUHYHhDM88azWJCbVZT7ikGxpAQw/cMulrXIR2DWGTclwv3IDQLYVKF+5r2lZBC5QASioPxKCek8HnqEse+xORdR6XT1GyE2dBGCSLGCCjpJAjVlNutwxTITrt4VpQ6PnjueQKAC+gkorLj5JTt98sP35f8ynP32ceLAGIQB7HPC7AW/b1cTfM+yPUi9lnfA7anUpYE5iN4Od4YdASE5sR3JDDRL7if83MlwhDLIL6AJA1+jn2S42X6icxVMiNPlTc++ANoBECIP6ERDKA36zvj/vP524BDG2gg1hz6kvg+A1CWhFlVAEld4ux10QCg57BLnHs9gFZrAoBc/+IcIdfoSOKgvyPxBACTcUcAduv+MN1/cD/9mz/8fY6ftL2gjnZ/ANAad+sKgva5h7EnktEm0LgEPaQhbAK1mM05r+/euZHu3LmbboxH6eT4mO2LOKXfUWlsBlKjimQDykxhkoJlnN+5FaDpkmQ9b2PKAQYtzxtBlDg/f0LAe0ntTRyurO+rsvPtdsHxgIoJmt5sowTfkkwRd3ud93mQbYj1pwuzoT6Zbu+9+156/ORcZkUd6YynYMIZAMxAaZRgl6YwrY0y/nEIAMSvzSA3AMi1qS+JAbcnAMBXX3kldToLzksDgL0ukvVNibK/+yoA0GsD1iF8lvH6eJxOTo7p8EzSwGKmkvqI343DZE29irD0zxUAxL7PNegLAIDt/mz2Z4zXZt//9QKAmVhVAPZlXFRr/Xf+5E/+ZBfZJOEAACAASURBVD/WWwAoepA6Ax//3kZt+77RyxU9NpOKAZgBKLvAxMZQL4C7mt+mfsYBxJnnKPXBhNKrydiJ4t3cIL7bE9oUzmYC1Mhx/k3rsFO6Dx969PI+vHDnzLjb5eoPF7/99TIA64NNjSyX2npl0FozKs3YbCi5DeJelq01LrP1wbFdc183h0vc/PNM9IyJCTL33om2p1SsrsOv+6e8Tv6ePQyXlgbAaJOOj0/S2x8+Sf/hP/7H9PNffiTX1DV04wZpREy0k26NB+kYIrsrlemCqeaNCt/bgXgrmFFzZWhPT8cE/r788gvMVG3WLpM1cCUwDRoZeEELjJlGLLzhlkTq+0ZZd7zIBAzxbJc+b5GRZnCgEg/cE8Afi08jw0RAYTaVdmFkdlh6CxBkLG0cO8cxELNOTwt4wlcry2oGIEtnkekjENIweNUuek5u+BFA8cAUmTsE3zLzgFHHkkEaSjL7o0HWxMP7GVzDNTfuCUAa2Ad2BMtMprWAKI9jA4DUHURpQphDDKkF1yfggtfN41MCYcgNP370OD16+il/fhSlretpBBTRT9Bm5LX6g3R2dpZuQ9cRGnQEqSQAzkM1+6TL8gICaFu5DOJ5BG7iMAJBQIn5Qh/J4B++H2wKMhij77N2TJVB43vBBA1g1Ycc9L/cAaXhk01SOhsGdLgHHECQgRKLTbMHACZ+7ver7Bd6f7g/acroMCHmHSpN8MwGn8FgK0GGktVEMN8Z8yLRUAa6DphxuJJIt9ihOMhYG9BgJsdjX8YeqOESwDdgBn2KrP/0Ml08VYmJzH7E1sR7ECxj/HcAALLES4E1ivrpbkfjGJVHkQkTDEpoCcGsYzFX4E7dtWvZ1po70n5smFz8vkgiWMsSbsBk5MI1E67TAcy4ZM9rnPdVmPV4L7gO/OOuWiTUSmCoXDvL6+X3l9psOfuJhFN7X8vgnuOP/N7GEbz8rqsAQO+5vO+DGe2rmfFlAF6XUO575jpOyvMuxq3nt4EkP6/HfK0B6ARWve/mfgRD8IATnYLudjzT2stZcqg1H6QRmgdEHOU+dADv72uAyjYQzMNZ4f7r/RNzXOtCe5/WAWqbunHQwnrwLK+aiVIzN7Omblwsfy+0wKpKFr7lGgagGcfNgbo9XpfXmN/k/aVgkOS2KkrOd569Nv8p3Jetv4fPlAZirXmxp5KlnA/NmlMwAEvGIpkvB1i3ZGhHnB0Jvvr+85qcn6MBAF2Ci7XBbQFzJ0qWAChDiVpIIixnYDPP0kv376Tvfve76dWX7io+2aikb7MObcZwmaeGYKwZAADhuvuzn/1shwHocYMEJNnYcWA9OpGe10ePz9MH73+QLhaKIVgCXACAOOAyAdTtpo8//jg9fqJKgul8nZ5//vn04KUXww11kc3etM80mtNIHGN7JsufplqrtMVeBWMnAiDbdDTE3oq2W7B94D6MF0zIWEGxPeO/0VNiVIqZuIFZHDT9ABgyYdojSHh8opJPMPfJzgFbKWIpzQcnZK1JpTEg9iTKYhdkVKLsD4yu2aJDcI3mKetV+uTRI17/9t27mvex8Ho8MPGHGBQmWEgkRtyncSAQWgkJ9Qs0xLwWseIAjETGHcF8CqCHNdgFQxNDl5p1NGBDcqNZX0qJgA4BIElnTCYXaXpxKeO3KAUHyIl7uZzNWGXxyaMnlKKZzGS2tZz1GXs6cY5x4XhGzs82gLEpmkuepelYlxACoHU8jX5HKaoeLBhCziRHTDGdY1zIBEOv9rnXlRooOdV1BUSjBJ060WcnBHL/u3/1TTl0pwmfZYQ4FxUhYW4DDTbG5VV1gBNA3rcAvStO0/iT3FE/3Tg+5vcMUiednEALPUzgtpCVAegcTOuQqMkVYMkmoV6LIqEexIDR8ITtD61KAlSYz1hraayHvU3jGbE15ovjLq0dkvLB71Aay1gsiA4uMTaQmGOaAO7QN+4XxNoY5mAAnp/DKRo615YWsnt99LuBVyeMDjAry31D3+3ntoxGszbzeeO8YK1BMAA1b4fppZdfSr2+9Kznk6eRoHZtfrvq5DoAkCMsdNPZhqMh15UX7j/gmWO5hDZ9J1eK7e5J7f2z8VA4HIcxZvKFIj45zADcTYS196b29xsHat7TxiOylEzlkuz3O6qpw8taMqa5vtbV/1YAYInzcF5WicjO9773vda9lwGTFlQ/4q8XAOReU9Tyu4GyFqF/vxO4x0TwbUWJWD5I7JSK6Mo1oJOBuPboyFqIdcDaiG2H1kn+nkOAYX4i/sUdUT/OIdHv6rZ+7SXAnwUALMfBFwUA9z1XeYBXWxWLW2hc+XOfFQA8dDgqJ3x9T9zo9lCL82Esgjj8e3SiMqg33vk4/fmf/3n66DECU2ScJBI9jMD/9njA0pAFxWhXubQRGzvt1DvKxM4uVVJ6/8G99OVXX2XJBwON+YTXdcIQTD68LJLrkocaAMyZvtCucKbZrqzQABFY0zCPWJbmTPUCYMKKACCBoABc4d6HoK83jI0bARvKIpwXsFCtDQ3ioMDgNrLXeK7MwKFbGsPYFtCOXFIbBKqBcIn1IsgzAIj2YxsXBxoEljTWmM3YLx7TLo+GeQaNJiKBgAMIXVXh/LZEgBBl1AMBPjYDuXF0IvbaFtnmiwwAAnjhZgmwDgf0AF5rAPD0+IQbMgDcnGGjC7VEfPldyCJ2UbItBoWNI1jaCnA0QFqWeETAi7YgwNrXwaXWvjSDEffoElm3F5590B+S0Xg5mbJfWa5UuLW53FgBFu5BpVwYN9bKw/2i/dDPl5fn/HN8ZLFmZSRLAJCBWARgAgkb0KcpOYuDZ8EMLNdX9ysOLNZtRDtIxxhZPc0nA8zIkJJJggNJHDwB6IpR100fvPceM+LT6YSf97OxbJji7io1Ho3BNIVLccMURL+ZmZr71gEMsJYtylNUikV2QjAVmnEgAWYD1gKjwxQmQFOvSQhUdCgSAxKBKwLrxQz3raC/tae5nCQC6Tq459ityn/zjpaNIRqAad8a2to3kHovXj6klwBgS+usAADNWNndO643fctAVrHxtkGw9n3tPkfDhil/lxOYscY0+2nNAFQJk/dNBrNkOWgcXwcA+juvAgA5/isQsNH9a+5nN6bBDbQBQAMj+wDAmv1X9m++dhxMsX9mgKeaq+V87cS6eggALBlJ/L7axbjaqK8CADWmrXHijcoHZh2EHI/lA2kwms1srqVYYNuQYzuDfEXSkKy28udVWxgw2BnbB0qA8xrtBHKl1VtfZ1+fl7Ffmd+0Nqz659kAwEPxaw0AmsFj8M+/d3IS16EMBDVZUc4mIHC5WIrR3t8yHvrut79CBtHi8gnX3uXCB+IwDYIpTDzUVQCggW4AgNhPfSAbH0tL+JOnk/Teu++my5X0fzfdIQEDmWOIMUaN2M0qffDBh+nR4wslFVM/PXz4Qnr5K1/WfjOIyg0myvQ9ksY4Srdv307P37/DPWWEJCDiITO6AxBfzZ+kyWSSLs4/UUVCJDI33C9QEXLMLvd8Q5xAwCMOrAB+zGxSiW8ksuiK201HwdAlIA/Tq0GfpaA3bpwyyQW8UUCWyoCRSAPgCQAQv1+uu7wv9BnW9Al1fUfp7NYtAWGh8ZnXMSS5o735e+wxTPC2tdXyXoOimKLiRVqc4QgeTC8x9hojESXb2m73dqkWO78J7hFXN7qZYuRy/ATAg/2Zr/gMAEv0I0pwGXeuxumTTz5JH3zwAdvl8nzCZ5HuM8BMm/rh74pv2V/BRAOgW7rMlgQY9mMwFesKPBwECJqi3DIAQPeP9pie5GAI5HUTGIQ8rxydkon34P5Dmu9M1gKW7909JQDW74eGW2gKj8MdGYy23IfFopEBwGhSm9GQAYg5HdrKg06HLLEbR8esnuh3VMrbTSFREokYjAfdvzdssPIAFqof3K/ep3q9kRK8a5iCIJsMZhtplxxnICo4RpIxn5KuiPttpicG4IzxMrQ38X2dGLfL1Twz5PH9mQFoaZ2k5DjiLwCAk0tJEyAO1hrnUswA7gOB8T7VEJba1RReH5u1utlH9p1p3W4GANcbVXUh4f7Ciy+k0VjnlsXl+bUAIHEZlwIXZ6jyHJYrxiBPcHySXnrhBY55JKDxeTOdd/ejdkXA5wUAOYcKaZTme/YlrcrDfJs5v/vu9vmyJEbta/caAPSZZR8AKFxD35grLbOmuc6Xh0qSy/WQf68TArkB4v5tBlTMVQOArXPx9/7kf96bG29AmRoJqQ7gBxDs5n72MwDzA22CIbMTAekHNVDVBIBmAjaaTmzASqslB+qZGdj+onz9ijEGI/DyVYpS79xTMUl2mQa6z7xwVM/52wIA3bZ5IOb7OgD0BoOhHqC9rDEW48QMnUJ8ff/A3j2YeCMuAcBdDceGis6FphYp/4LMPzdDXfrLTSkyH2aTkXE1HjEz+MYvP0h/9Vf/V3p8rtK+1VKZ46OETN863Rj3qaWHDYXCnH0tYDgAcAMK7SUAe3jfwxeep337egUH23UAcE1JKfc6spuUIQPw5iCOizMDIAVFLjfkMxCLUtvj/jrdgQJXaxaEVhp+z8AB5cgEG6RRh42b7nKhgbPpqXzskIYW16qCAYUNwgcN3O90ofkvcKbZMK0hB60cXiM6Jh+24uADbR33C0GiQZTYwFF4IyFnACsISm/dus2A3RqFHJfIxoCpRWfmTnabQt+iFJSkGASnmMEIoCMpgmvg/TCHQMkQ7hLtOF8ikFiFiPcm9YMRiQDBAS/6DKYep6cA/+QSzPaGTiR0kCJjyXbu9xiQ4zBDlmUBpFBseRFuyyylVemwgOJIlBSumyWDKbNY0TdIoqB8OoAJuh73wJ6YUdwYz4NAji7Iwe5D8KbAKhipBQMQP7dpBzLNeD09f0zNPwSkLD2KwJMZb5QER2kOGRIc3DoQyMhGtUHsn/ivzGTVB3B9v8p1qWtoEJrjWKw5vicCFo81jjOanfTjEKrSLLgAfvzxRxwj0AgqD/UA/ehUfTQkEAjtP+k6asSOjsCMADiux8D1qa3p+7IL6ErMQICurfkaQCTcrwkAIrgsROwdCHg+mnEBIBXjfjq52AH/uE4EcJxLTPdk9z0/qi2rYgEeoNZF/9UARL13+tq7TH/9pgSBy/vwvlWzlOrr1EBd/Sz1/dT7dwu84Q0180prmwEIt4MmQulGm5/R4xeHI8cjhSupxkcw5HcSULsVAOUBI4vDH9AxLq+t+/GEPQQARgKjYNjvAwAdJ7kkjZpudCXVml7GkeVYyO1uTcCKbeZ+qAHAuv9q0xL/vu733B+V2HiZice+lJ/DTJronxyHVPFuCTjujJXYQ/eN+Tq+bgXlRabez9NUUDRtetiApt1K9RzUvGrmbTmHShD+SgaggcwDcbVBluY5Nba9L+RxTlMClP7K5dOlwEhcMU5AqS00ttKWAMJ3vvkawaft6iIdQV85XCfL/vbfoY07vVzuZQB63Kw7SyXIqDfWSaMhjJ8W6clkln7xi7fSIo14cAawZ/YT7hfmN0gALVZrMgUfP1Wctu2NKLr/4quvcB9DghRjGG7zmoNK7GFfhPba83fBIFulDnKhYLhF4gulcdLaWvL5B0F0gNQK4kWvc9MpYpxpejqZkrE+XwRLq6OE3eVC2mYZiCjOR9gbT5Yf6b4CgIM7qONEAW9KjMJ8i3+COdjrp+OzE+13bK9V1srF3gVA8+RIzDfsi443y9Jixx/dkRnCXlclEaIkFswbwvQh1s6a/drtDMikk7TNJpes4mpOMukBY10O5rHjuU0YxqF9ENM5we2kMo/svJcwKxoo4Y8EOBmW6z6TwwCWWFoccaclaGxqA6Yq79PakxGXnU8BSEm/mlIrdG/G7qDxcn7xKOZqaPKFtIilGzAvmNB2KWxXxm+np0cCqNczAuZI1hs4xfywZvN8KW3i7hbJ13U6HiqegWcE4+b+iOPQ8Z8TiY7Dpqu5QHHcsRngAZLbbAVx6enxsaSM+kq4Ho8FtLPmu9hXUUHi/mriywY4MUHAzFFL6ijhiURuMCWD8Uk2J6sxxEYkOWOJMm7olqtPmWxezQT8BYC1CVxiMVeJuquQvN6bAYhxRIkhlL//8l1qlCu+dFwf4zpKryXNVBBMKlOpTXXA3lcN43Me7t0l5XhqrrkdmVm5JB6SAA8fPEinxzKLXCwF8ILhyP4K858co+yRXivnbX0/OL2ByQzHdq1XmgP4LrzqeKo0weLvD1WWFlsY16O8sTeSUoxiHNsXTPOdGKEVE7XjNzMAS2mP8vNNnLmbKC7N5LCbtohLmckaw3snvgkgMNzlnQD9/ABgG/jLzVUBgKVEG8fBH3/v3zIS2BckeJK3G/Q3BwDuC9afBQDU/e8Xa86fZ4ZhD60rHq5myDgTkwOYHC9VB4BrRZ0jA2DqcDmwqS3zrK/dAwDXzS9oApLNVvJt7AKAXIgDaDsEADZus/sR6N2BfbXrTx7AlYajJ6z7tSkBbijZV7Vog+I789929fNn92n/Za28CBZZOtnrprfefiv98M130n/9r/+Qnk6kNdHrKgAbBxB3NkIGBsFKZLL6YKmLUafNSUDKjZNTlpDcvnPGjR0AIBlCc1DVVULCfifjCkGFMgfYEL0xECiK75XeWrN4IXOMzQwBwcnpaQKwwMxhaMqRNYSgEVlaBDoG+lhGoZJCi1BzAQ4A0AG1MuRNmbdLH81MszivNvdtmgWQ1IjFtsd5BhYL1qU2dM1nAKpynJKGlU0j5Ia8JZjDAKfbY8mKTV38/dxcgunEgwgoesSbeml6ecmSFzIkHUCGK6vAMpmyACjD+Oam11WGG6UEKI3pLKKkJgBABDws9e2BOSaDCn5fVxk6bNgIKA3sjo+P040bZ7wf/Gy5ESPCJabTCzC8EIDJRAb9aHYbrum/1+WLJQDIdR6AMCz/gvECMxK4NEOvju0X4Le+23pIDLMVaIcZQw7gYiKB6YDrT6dg0U0FcoaGoAIEjatcCmvTlGIYUDeoKKHUeuSNNQDiYswxwCoYUdLhc+mNnpNC7WOAk2IfcMzTCAbsRwkqY8xCtP1Xv/pV+uDD93n/YH547Gic4DAHDaYjXhOuxgAAgb9y/p8cMbBXqS3WvHCri3Xb4uicC3AKnkzIUqWuJwxS4oACzSO51QUDMEqlDEBhHWHZHKqBtnjvjONlPr3My2GrDNjtU5R87gMq9pUFt8uADwOAV67DlbTCVQCgr1OCE88KAD7r7ur3lQFrGWhzraPGUrUPFwdLXePZAUBq6eSSLQMEzwYANrGM7+f6SKId/+wHAHnACjdMju9KYmOnBNiBrdd9u8NGQG5n6zqWfFYA8Nr+qwG5FrDl5HAByO4keh0TRlyXS23bou3lQbW1lhYHuKuAvkPP0QBkV/d/GT+VjKUcJ10D6B2KfTXG22NnXyIW31N/r/adNqOD76vYi/puGBZgHa5icDBl0IYo0Q4zEO4vMS+GYQ4FQOJofJQePn8jff3rX09fegjTCWifKZ4y2FOuzQYAUbKKPQxagt4XHFcAAGQ8Fcw66Lkipjqfyj142Rm3AECvfQYAET/h4P/p43Out70R9LAepNde/5pkHzoyi8D2Y4kVmX2NWRL52iu35CK8EgA6pOzUNvUBAHa76fh0JEbTBuwlxG5ab5mIYuwAYCeYYDTnQkyIRKR0dtc5flaJ8WSKpN4Fmf343hvbR0rAAYCiiZe0lOFSj7GB/QX9ovhyk+ZgMSKBFwBWfwiTOxR+KgHJz9CVPg64PcWDiG2OT44JFihRJobULBKvBH+zQUezxnrc7owbl7/D9AL3SqMQxF1RORDbUj5TFgu7xrzisoXjhtAOVCVAL5dFf/rkcZHohJSUtBj7AABZMaL2BzOxTHh4XiJ+pLTPKjSC12Jmccxttqk/PhOAyCoFyRSgHcEMZMJ1oAQpmG0c272G8Yr7B4OKbZcGMsJbgw0HORMlYMZHTUWGmHJhxrZcK6G4VaJ3PsP4XSYAz1wPIDeyWqdBT4CSk6TWNHYlRBoqcS/GW5fxt1mWNPrBGD45SXdu3mIi+2QoTcqbZzdkNsKYs0ms5RLmPATapej+vQkPZvKuaSYFdoMYg5beoGyRNS0JKKM0WOAf51MQGxKBP5ipqAR/QzOQTXoWABCJgNV8QW1MaGZzHIaJojXurcnnc5oTTdkkNcerFQOtwhfMhmWTUbs64slYVjtx7jQA2B8N0727d9PtmyeMDwFks502SyX+KwDQDNgyHitBv3ofu9zoLAmjEVwf0lJIAjBpEuZ0Tfy2aybzLAAgPt+UyrcrMsq9uJQ82bffahy0K8w+LwCYE1zBGN4HAHrP3JdIa/AlM2sDuCZOdDiWq6XaeP4qSsTrBHXZDj43lbFABgB1s+3NeV/QsPOzGED1z73wmlmUSzer/X+wFgMoAzuxcTiwaIKqAGycgfeE6QWzBqEqggv/3uL7ReTEjbJVCoTh12SG2AY4EEKToNLOaQJh3YcnCjKX+vd+gC5n3qrnbsRArw/c1YmfDQC060DTL7vfww3ErlSVC7BFZ/35HTfnuJ9+1taIADrbeEVJjctf8iqg+7Cr4L6J2voZ6fyizvNgX2WvESq2XlWAv0vbPdRPsQ8VWj2+rq+BBZPBA7Vg5CI163bTT378k/T33/8RtVFmIVnRCwr4gAf2Xro5FmizXC3EdBvI9MCut7guMm/PP3eXme6joz4X0uXinPMSov7I9NoUAwt37puC5VUCgQTFQj/Ph9kNSmO7HWbPYcYxX0j8Ge8lczDc00rtK37PeqnA1EAKBXBxgG2/vHiVCzPAnXzM6sj9DhR/ZoqOjsPlTYGyX3kjigOG1wEE3thc6HgFw4bpZQanNE3k9ooXgJy7d+8w0LWTnJcDs/4gFs1gFEBnB8GzA1iUACNYnvD60KJpMWDiQDQcdhRARWCMBZl6bXNpNoI7wIU3JGgwLvAsq66093AQUMltlJQiSwfgJoDR23fuSGuoEyBVaPCZBfb40VN+HgxBOuvFOlQGPpw3FeBSAoBuc5VGa34M6ZCGIBEaixrUPsDgeQDkug/wO+o1c+57fkXiI/oC77d+HcHEYFSCcVACgHRvz2zQhhKf17G69Dfa1UAY7yU0XgUEKkNr5gmD4jEy5GAwyKGb5hzzuVwPEdzDOMYmBt0exb8hfo5S4PlU4BwANx3A5Eh9PBrnUmCZjwxUOoYtiYwGZdRpylMcoJQp1iGa/R5Af8MgEIjrjD31k0KAm4F5BAu5sjGa31qA8ymkAxDQRolclANnk6yYECXYVQIZh3QBsxtwuOheu47nN9T7ULMel9/r5znEQmq2k6v3z0PAosEM7//5eqV4clFeWTLSymfdRryS95m8/+zeF9eBcPHWHogoo62RdzjY3j0UP3ubH95vbcK0YwIS4wIVF2Vst8MCpPaa3Fw5LmNsOtPcCc0yX8PMFvc1ElV42Ym+jnOac/uzJfhKBgXHdDDbdzSso598P2WcRqZMBmYjceB9r4jD+AwHNPBq5sOh+Ng/r//EfZfsx30AINqt1LD2GOScyRreTnTuAnVc07EO0/r56vhVJZRN0pRbbQUA1sBfOT4d77XK/MmQVBwDzTqOHbKrAJyJuT3oSq5j0BFgcXrUS9/59rfTt7/5VZov9ToXWu/DTV3rvxItWH/ns2X62c9/lqbzJU06ANhpv9K4g3YY1nFLqmAfxvWmy0364Q9/RAYg4oNtJxgt0QYAaKDxPFvM0/sfvJ8uJtLK7QyO0p07d9KrX31NjL4BjC7A5vM+oNJE7PtwznztoRj/WfYhl4iJkbhNKkHE/ob26fXGjEMAKGE/7lLCpXB8jfWkOdhHXB77s3tZBgbdtJqpJBBMfCadlmEiNZmwvc8vL8RIT0qkLjpKROLfZNcHU8qVQAO4sdLtVix7AIta62SI1e9Ll9fzsn8M/eFNmoekDfZO9NPJ6Rn3U+kyi2nN61VSEp2Nxk2ex9G/lvDw/rgOrXmNd5lE4HWxmAW7TeMM2yQTuLHcEKSio2+cj7cCUHw/YICWgCESlTSlo/EZSsZD8iQA1BVKM7kfx55PrUL+T89JxisAQJXxAgDkPt8Lw8AkwBrziaYyJAKgPUU4QIk2+2k5ExNtIOkbaAki/vA5FeQ3uSjL/GI2lbs2JFlQdt8NoNkMFRNAsExwnvYFMCJ+dmKxWcMEAhLQ7Yjhivjo669/Pb3y4itRMXHE6pJ+P8zswgzE5yGsX7qegGiZPgKAdYJK7Yx+kHmNTF+s2bchkBnYQJi6cL6TrSk9wGwsGC7ArPQJBiCBcAKA0vZUn8lLQGfAwA3SmucZaJW+8847aQENd7JAUdGDSe9zsdYPSDRx/YnF8RDjy22ZtVb9/uKs5+fTe2NtjoQK9lU8H9au27fvpNu3TwX4+TqbhQDsza4GIK+7pwQ4f7j4y2S9Zn++8sorETvLPJEGR4VedXb/rvaZZwUAy/00A+iFmY1vqTwH7LvfOr4oNUj3vf8QAzBrm2YAsG3ed1gDUN/SlAC3E7gZJzsAAj4LAFjG0eUzletubi8zALWo/GYAQA74OLjVbri/DgCQ14c2xBUAYF6crgEAXav1LAAgF4YMnP12AEAH0PXgNbew6dP2gcSD5IsCgL01zAmaBQgbBQOWCJhyoJ9H3GEA0O9tjcMKAKxFO3+dAGA5cXJmACzN4tDtzQBBIwCmaadDAPA//e3fUQNimwBuCQhhxjsWw5tHypBhYy4BQFC4WRLa7aZbN2+l55+7Q20OBxyXk08JSKzhRBYAoO6hYSK1Dux2N43MvkXC7RYMAE+lkd6IIgMfZZg4uJH9lHoZNDOjjYEOWIfYCBHEYpHvqFTY5QUG2vJGs1Xp4haBEsoloYV4BOdbASLQIASANplIO5HBDkAKr0VFyR1+h5JbfC+zWRDxhRYOMtc4LECHpiiBxfdgc+L6sN0w8w1XStP5L6eXaTCCqPQgu6iaWYkM0fLF7gAAIABJREFUO+4bmXJviCwxNkOECz/AQ5lILIL6joCFWkZrMAOnqU8BbzAFrXmqwG9FN74ODwJobxlJDPjsDMwCAAQYjNcitGCgKcQDU7BQP/30CdufoCoP4AKUxGZrWKFXAYAOJNBWAH4xVuAyiBcOaAiY8KLYNcBUMNrWCzEBY6NippPf74BC6yHaAIEoSrWdFRQorYMnAEBrGnIPioHjAAsmG629qWaXgMFWaBxq/AiQ8MtGBBhveLFkF4cLlFAXLrhuBzIUAzTD+8isXK0IAj765FNqMmF8SuNvRoBxDNAQgfsADswAGOXwDHF7zVeMd7gVglEBIKjRvNT4FBDJINQC51F6QgAbJUbBytABN0TcQxQc80sNhTGJrL00nSbnT7i+NAxHlUfX88v34D8bgGY/wPabAADLe7gKAHS/knFQGIrkDi/+UgOANQDRMEljX7oGAGRflmyrZwQAvaf9cwAALbTP4RIu2FcBgOX8qwFAl1yB+cH5H+LvuWR1DwDYDlDDgT4OwFh/yv3/KgBwX3lwXULVmIhV49gAWT7oWIQ+Bk+1jiG+5PgMBrCHWI5rqwRLeWDRutiOrT3Hm0PzLgPwXxIAeAj8MyhpwKSeo9gvuJ+FZAIkL2g+EBIJACwIJmzA0N6k42En3bt3N/3O17+cvva1r6VRX/tzuYeYyU3Nq/lqLwBo5hA0meXG22Zcz1Yp/eAHPyAACBBqG+6zlg8xAIgEwnvvvZ8uZ2ITQSsQlQavfPUrYoL3tQcPRv0wRViq9DF1eTB//eVj7t001go3UrRRPhh3LJli0wQBlN3uiO2yIADTyIhA+4vlq2slq3thQoHnJbAUQLb3knE/GPQsAQUDSvsImGXoO8RH09k0jcZnaXI5SdP1ls87xfPG/s15sZDpFBlUUQHBfddmbpbugIYenpPAfDdN12Kom2HYjA/tm9hjleyERAlcThutXoybQXfA/Q7xCsBbxLc06qKMCeJIPZ/PaZtYr7M50qAf4HGc3+j6jb1Zd4K+IsMwNOFcOukSYmosE1BUAj1rbgcwsIgEJfA0jVPFz9sgXsDPmmsBy4Ob0m73/3CEfhuk/gDxHNA3MUMRZyBeHDCxuE5wmUVcj362yzH679H5IyY68X6OBxRJ08RNpb/rueLzywmYjt002IKYIACQcb0TytQ+3KRVMAQNxHYjEZ/Xw7rkMEmC6ObpWfqD//4P0nO3n2PcPz5SifLl5ZNgFwrAbIg0AgAF6Il5qX50P0UifbngOPR6mgHDMIfBNSjvE5UlaHs6QgPUjpiL8xYMTIzVPQAg+zeIFLUG4Hq7YiICFVpvv/U2K7nEMNQ5gOdAnInjPGzAqSbUlOtiuV9kLdqK2V6ela8DAFF6fffuzZhHoZ8ZAGBatxNDLEG/yp2+iH8QCwAABPng1S9/meNlOOqn6VQ66zjP2QwwA16fEQB0TF4DgH7+ugLyXxoAqNgKYzpc2fcw8sux8WsHAL/37/64ReKpATosvKZWGpHnDYXGT28rinDNdEPgpYU9R1j6mOm4EQ/1VpEhirdl5Lsw82gmd6nxp2ZBCaJf5X34uxqAbH8JcPHxLKTNQVcU55bBW81cq9urvB7vqzIlMSXfDCsE4Fe9HLbWgbE/43bO14gMWIMwtwPPRuNGn/CGX99DRr5DVNjtWT6vgZG4kh7Xoqc1Y7FmRjgAL0SbNak1Ecqxwn7OQtoVlbIqoo54Jpd6NDo52jjWsX9khmlkFM048/f64Ij2LctYUYKJl9lDF71R+ru//7v0t//3D9Ljx4/Tdi0B68FQzKnBYMWyh2EnSiVy6d1cme4FhJLlXoVr3rl3M929cyd1eygJnCbExWLyqczFJap5/HRQ7gd2XsPAUMASG6bHg5mq0XzOjKLEhf1mDZjQWsP1EZDQSdZgITPRAui80Z6MT/ieGcRvY8EnQNFN3BgcHCEos4EBSgLIcEAguI1AxKXRZleG+1sGZRBMU7NCAe5iIe2UvgMhgh7FuKGRrkoSrP1BsCfEib0uWXOFhwwwAMN0YTAYsUQBbrA0AEG5BkpJwyzFZdXQ7FOGPMw+InNpd2KbuNn9LwPuUWoLsw2NOQ3MJZyc4d466hGkun3vlsZfklaRXX0xHjC2Hz9+okxjuP2SrRgl0dTmgR5XsFbVz23GskXDCUaiBDkC4lLnUtdU/wGoVVAMrY91GvWGDJjFzG2Yuo2mhoKzRqNM/4YrH8fJCsxG6QkiYPLPrbHYaGy12ScZUMlMJYk3CwR1yYrGA+7dgAQPCeizteYX2tDPnfUG2SEKQHPJcwDZF48v0qPHj9Ll5WUOlHgQoWELgGS5DUPrEPP51q1bAuSif8D4Qj/i+xGYunQX49RsBzyHS1zILsX8moKhuOL4N+ND+5REry1hYXbyeIhDArKwS5ZxXzx5SqDd/YzrUCQ7l1bHQSQOMw5C7TK8CQZFs0+Eq+ceil0JEiFgrsGP8t85kKsAlJw5j/U5Z6Sz3XRjErOfwdQ+0JX3XX6/DxRNplfZfZfaEMwPBkEbuNIVSxdHMqjNiO81DpT4PpRucVzmNSrGc5JG5s4r4gYDyDWDrY6j8udrjcL4vibTvr8SwC7uTQlJWxqjabO2dAeAAuqaBfi3LXTeYvJpfymY+TzY5X3IrHrsg/vc8fRkO0Dg/lYTQ6QYS2CoqJ8y56L1ycyK2NH2c6DQMCTYjxUA2GbmRVKtKH0q9yR+3ppFsS/nz2fGnsHCYNx57w0GUd3P12n2+f2ZvZev18SdLZZt0Q4Yzwa86igVkhNcJlEiWjI/q37xPpLboZAI4edduobEFecJ9hMxmqixZuZy0kH7ZNwncPDw/vPpK699Jb30YETgx5poq808tL02cr2cL1mZsVx7T4DLO0CzoWQnNtNcusi4qSvt5uWql/7x+99Pq22fcVe/dyStM0hYoCQ2yTUe8T5kG8BcJNDQxXfO03MvvKBxO1Rc0hvqvvG8+Pfx8YiaaM+fyTSq31nzeeHJwXhvA0MGmH/JFddJH5umQTMK6/diJW23klnreVTH05qIXndUurrZzrOcCgGfWpQoxgsY6Lz/dTd99NFH6XhwxP1oMJDOMvY6vBAfI1F2+55cgB0XoA3RLmBiMsFJltoizc7Pub5i/ZHsheRQbELHNmyNGc0Lj6ehE0AsicZaDXBrTcAHc7u3XRCYGI2UGOsN5d7ches0S3ijBLUzlMbjFtUCDSgCBiafAwApzcOCge/lpBfnA1aPSHqH+3cSEIdzhhLmjfszxp9NzqYTGcs0JiWhsxwuzZC4UbmtJhbCRPZTF4zNTXaFrpnjjpvI1gwmHNfpANQFfiGhu6LuMEqoMb+G0CgGUzM0zIZDmWyA6cr3o8QWIFoAaAbus/RFsFk7ASx1MH8Anq236Q//8A/TCw/uaz501kyUItFdvkrFp3K99HtKsyg+w1oJ66xFHcnTEhAUIK59AHMUDEevK72lnsXaeKpIkYkaKze2eO5VWi8FBGvNajsig3iwXi7TG2+8QUICrof34L4GI+3vBnwdb7hSq5/dgv2EDdO6WbuVWCulOXLiPXR2AYQT4A3GNOYZ59FIDEBoQpJkQFOiVdrgXBeEgfJ7quU7V8LUP/e/J3CMHo3SS6+8wvh1cDzk3AYQj3kAQBmvGhepoxCfMWvmfN4vDzDiTDjKrVfBA/tizdZ4C6Z5/b07FQPxobqStVsQCA61EdePSsrM762/10B3/Tw79+d4KjRFD333bsxaaAij///0j7+3bWW0dxqwAQA1+INCH4F5DQBmhNylT3E9NFxLQDJ+3rftdlVLaHFLAx1+kMbkQ4+8zhkdg4vtUpkGIW4fIN1g1wGAu4eX9o1+HgBQz2LK7vUAYOv9dSY52rGZKDrkXwUAehHDn78JAJCgTwEAsg0/JwDYIP0GAWumZXspydT1nRnx+QFATmCUv8IJbaPF3QHPuxez9Dd/85/T9//h7XQ5uUwdlowMUn+oUoqj8VZab1vZwmegcSsAcDmVdkl/2CFYcPfejXR6cprWW+h9NeLKAHy4qeFPAnxROhUHTrmT1SxPCSqzn2NAZGZoBNarbmxQUUbjOQomBa5ncCYDKzk40gH88lzuwBTRRgCLIIvvcQZM900QpthAYf7B5+H3gEYvrRJv1KO4DoJguhCTeYYstUqwrXEItzqx55RBFDttlRYrUfhnS2ldYJPmOAwA0CcPHjIisIOrINuKLn1jauBdPD3XgSIAwH5HGxu+Q+NcwKwZDg5EELjNkAmDaDQ24qSgoRMagwjEKZobO0oWKg6R7+GR3GVPb57ynixKDO1ABC8I3PCs1reTaHqwJ4PBhjKHfeAfnzE0+5pSf2SakT1vlwR4GhkYnE3BRluKrQCW4UaH/+Yg7HXWpY2NZo3aTMxEZ+oWk2kOmHjggVtiAHl4TpcQ1tT9vC6Hhl1ZSmDwiu2BkrK4P/ybYtYAM7cy0MG8YIadOoHF3hGlLmZXoo9RsjKfzBgwTy+nDCbtyghmKpmrYFTx8CFNSIxLzP+jk1MdoHpjsT0gwxJMS2l26sDCw2wEDBiX+B0OWNNLafplrVUHujTVUXs2QW9Ko4FcKrHuQstycn5OQBvBqdaDKHO25qIZTkUAjfdprjUlhc2yehgALJfepgSz+em+oGwnUDGQdQ0AmK+6U8LYHoflfbf2dBzmsXYVa6dZvviMg3kwn/3K5fMtYCp+6wRAVwcI6n8VAKBLy/MhdquD887rAABYxyM7JUSV+Zavi/mhzzZ7RHOoUAkly8QzgPj5AUCzADl+bEZUmW84EYkSPo2z3ywA2FR0tOO3zwIAauI04EAJZNb9V8dfO3HkZwQALYGSwTqPD0vdXKEbhHurma9NPFZpDu0BAF1613rGcO00AKjv0EHV6wb/LAyY+J49ACDXabq7dtKmCykLKsrl6/Az0L4dDNLpSEDPzdNjaiV/6fkRZT4QB5CpvYZWMvamJd8H5vSbb76ZwMTS96g0zQBgJwUAAVkGWcUTAFyt++mf/umf0nILd1vsswHUh6EZgGXsV/2tJEAIDPZhidNPn376KN28e1eahqM+AYceqi56cMyVCP9wqHH04GaPFR+3Tsfp5OQkDcMsBP2DmKmXTSrU+g1hQIm03uCOhmUMa6zVjLM8vrJEj0tYDbgbwI3SSoJXWwLlaqeIL/N5MFyPp+v03vvvp9PRibTqhhtq1qItkPBFfMy490TuxNAg89jH/SKx5XGB9h5HXKhksM5F2PfAIsJ/SKpbt0561wZhtB6fIPkIQCriqxIAZJxODT2sfTMlHpFIpTSAroNxgBfGm8cH9m2w98kMTeovDGsbxhkg4+eQ8KN5UDCr4tyz7gg41rqmOLdMkKpsFbRAsT+xrNhkDX/CTIzzYRP6gBvFe5T2WMFoRWYd7mfHPJ6jIC6wvaPqwWsA4j73B/a2y8sZ9zgAfkyEggU4GNDFVftD3DdLepVQx+cZ+yIpGud3mKL455zzwUCdXiwIkK8Xm/TyKy+nb3/zdzhPTo7U7gAUfT/YdzO+UK0b+RxoLR1XTawFjCFBaZae4yAD4DQ5jCQ4YjZWJAUomuahAwjWHu5lq5jZAOA2qUTaAGDGIYr1lv2/WjHRAABQmuPRTtQXRz9HXG0zTWvRlRUHeF+RCEL72PRNBIWi1LfS5Ade4so7tOecyf9+6g77CTJCZ2fHOpt1gy0ZSXdrfmt81OQagc1XvQAAAjh+8OILNJvpHw1EGmH1UI8l5XhdBwD6O/Bt++JDVxrU91JXQNbx0L5r+RqWiijXz+b6+ytf/jkBgOzva0x4y7haY77N+Oz86R//KbeOukTMG4A17sqAQY0UmdVKBFi1+tJeaV3XJVlVCZdFfHGQLF+1pl6JoHLCO9NbQaNNh9eAn1MoVQPExlk3VH6+nbLoQ5pz0R7Vc+SDq6nUpJc3AOAhpDkv5KE54H+jA1uDeocQZ4bP/mlbZ6yh1cHJf2CWu5TMC0RJXWYAkudJlOJ5YaoYgA01t9LyqRfAggFY3lJj/tHcadkO+TlMOI0PuwTFB46sZREToRMldHh7GazmErT4OhyQcDgXBX6b4O6J15sfnae//Mu/TG+9+Sg2fG3Mve6UgeGNGyhRgE28KOs5U7sJF9W5TBCOj4fpwYMH6c7dM153uVLAgowXN1tuxKCiB1sP4seY0NCeC/MHOaY1GSQx7txe0e4uyYiShHVob6B9DL5x3kbGcufgEkADSmYIyK1CmyXMSEaDIQNDA2Ll55ExxoYPAAntCVFggXrKPqoEVdlRMOewgaxWG2aaWa4QwYw0WcKkYQRBaYGOaB+z/RbhtozPUHR6DB0UMcGkqSNGgLOZCNIBADKoRQA0gLvdMk3OL6gXQtYAWFoGSsIMZBkBEINCMO8QiIDhtZwxMAbTki5qzgAtiwM4y+WleQfXXbSVGSsInFmqeiJQcuVAC9lGOMZOl2wXMM0YHIRrIsaaGIACKQ0A1gAL4gyu+ZFZNOONZawBJjpIwp9kq9KJFiLHzpI25h/NXA1zlDz/cVCyODDux6Lt0koBYE+NpFwipPUI/USGHEvBGo2qHSZLLGhm+ORgMtZt6xRyxQ2BaoK+ODAEUIdrGoD1c/DwhkA3iZmLgyQ0AF1KCzANpcCruZ6H4xUgYhzAcISltlKYuxwfn/KAh4ww+2gwotA4tBB5YI08ULk+4+CJEnGASCwtYSmvAlKX9m9WCjaNS4j9u029PgxwBggL9fnFIqHk/TIYnA74ywAQ884lIPXhIfdvoRXrkqRync6HvWJD2VlDij21CeyjVKveN+xKVzFTzGDO4poF055yBJxbmnvl2l5vc/nQ4uTEHudaHcQ20sBct7VKbX5j0XGte0CPcfjbpE6/Kf+0LpL2UjNxgoFSM+ZZiqaEhV5t5l12ey4ykGzLKiDMe5oTQLGx5T6JxA3Lu2gC4O9rM+maPtzPAMRt+NDTvu+4/eocUcdp2uMw/vzGOh50z+0PzP1bayz530uwcOgi6M+177/URdJA0fc63ixLjKjZWGnB7lRUFOEh180yqXDFuHflgw/yAChKplOusPFCcU3gX4/zcsy1fmcTm6ub9SCDwffYMFubiW+QoPxdDQDWLu/JZn6OI2FogP2DOnrddBySHhj/TKz0J+m1115Lr7z8AoE0lEyyzWGAEcmTt996K83nKqHs9kYaZx2UqCE+iv3ILuhYs5Eg2vTTD3/4wzRfS06jBAC9T2PPgKkB9gQwmQi89UdkXN+++5w0CscDxgGU6Oh103Q6EQOqh1K5adrOHrFC5IQxRz8dgZ1WaALjIC+gTQzCk+GAf47I8O6nRVowqQVtPZccYt1BnMqktUtiYjw16wOYWTJ6w7pmF92duDcSw4hPIKkyv5ylt995J40HSmzBNZXlrwQ1B+nx+QVjpZu3b7HMEwxG7aORwA4NVM//bpi1ucLJCTrKuwSTqhyv+LnjQeyFjz6Vdh0Ym45HqTXM9WydeqtLaRKuBeSlntYDlwL31xErbRsJAPweeyfa+ebt03C5jVpeMD9DCkfX03qB9Y/jPVdACQB0ySeLurPMQXP+G/SPON4cz3qdFxgGIFaVAgDY0DYYP4+fPKEmN7X9KnM0t5UT0UiY+6UkvMBdJ7HPLy6VpAqzPzDaGXcOcYaRGQpfeV/Uc/C8AIIA4lsmWivgZqX9o98ZyiRvqQqa7377m+kE+sthzseavgAVywoMnX1U3t48U2NY5H2NZhehAag9ROcl/8fyYJqoSOsc4GnJigQAyPV+g58j0T/PACD7qyOm7XolHXEwS7WutRdMXPvNX/yCZxuZ3QHEHbA02/GrxofaE0QpxtcVflJWcvi5vWZqzrT3Rc8rcICpTd0d8btnC7Bz+6k3GqZbt2+ls7MTaVt3xIbsrMUQzESksnqqmHB5nlY4iPGiKWP3bnruwf10+9at1B2DWY12xJrZ/1wAYDnfs4RE5cng9+wDAPcBma1r2oSnMOW7jgFYA4sOu65jAO4kaCsNYcTnrfWtAmKaSon2jp7jzQNxQH3e4xq1Jz7MACAnQTAvWo0VLeMvbAalbtxMPf/c2gA7LqoVAGhmUj9Ki64DAO0C6QcxhRYr1/4DRo1cx0JdHzAqPK9cQPl8v0YAkIfywpVL33VdQGsGjcVSrwYAsxh84YJ51QDLIvLt8ZX/9SwAYDmwslvwgRLgXEJtRuA1AGAJ4GlQ789I1GL4ftc+AJABgBmYBQBYNkGuGAqXPx6qgwGIvwNQwuvNj56mP/uzP0uffhRA3TqYN0kiynfvaINHRoylJDmwDfObmQK8O/dupAf376fxkUwglis5pjaaFjqEOiO3DRFhBAh44aBjnb/SMc3afLiOAUO2Y4yP1VYHUJaaxKbLP2PlMfCD92gjilFL8G+dbt++y/uaTi/TZHLJ+cLME93jFAT4EFQCgNiMYE9vp1lmIQkchkbMKty4IoDxdRws2ShisVAG0Yd0Bk0IVvpNGSgzUQTKemkFhlnoefgzeCJn7cwm7PeHMmW4nEbQgMwx+k8i5QwEkIgIBjKD/QC60P4A7PD5y4un+xmAAQKg1EUaigp0XDKJAwM3bNgCsrRegQoOpHIDxDN309GxRLURgbmNfCjDvx3s1NMb049lNy6tj5LcUh+PLosoIS5o7hCZzuX4BEUE2CJY8vxUf9gECIGINOl43wEAAlRdLhcsOcnBGkuBwLBVptkO1zFAW49gAIVJU7wfM7rS2vJY4H0B8KVLmfQTsZ+g3Xv9tnYZx7lL2nDg38jRDC680m7SC+P94mJCZh21e2Idh0s2D3jBpCD7k07BcD8cE/iT27P+jW4X+KgDGRirBm5d8g4QEwcw9AfKllF+DECQge8aZb14Drlzi8kFRmq4BYcWJOYV5utkMmWJFg58LNkYSEuKO1EwRzxvyYytmUXXAIBuHwdI5d68b5+uWcs1w79xqa+Y3r4vMwL2AIAHtrRqHO0mlBioF4ZEnJfRriiVKl8ohSRDKg4ePgjJFwMCqXp3DqSzvMXVAGDO1O4BAHmt6IesKeSy1wMAYCmq3uqTYIAaAHRCB99fBpFXAYB8wDggeu5kE5C4/7q0JWtIFt/zRQFAJjsyI0774r4S4KvGZAbaHGe0GH/7AUCPB643kVgrf1b2fz0mmzaW9EkJAOpzNnho1kmNN43buvSvNTaDlVR/Z4sN+AUBwPIe991Peabgs+0wAIORFAkXA4BZLB/Me+wfMCFbb9II+zhLg5Ec6qYbxwJCHty/m17/+uvp/sO7XOeWixkTWViz33nnbQKAePX6R1ofE5h9+DMMrnzAjD1iueqmn/zkJ2m2Uly0DwAkSBSlbh9/+olMDcYn7LMXv/SSSlL7YP3Ntf9Q3xcVEwKhcd3jrkxIRkkmGcPQvHWyE/uE2li92A3THRzkGTeMFS+5IgRAIEorj06P0tEYRm9Nqbb2QzG/wXzDfSLuIoDjhJlNCqzRt1DiFXEKEqmr+YpmB3A5wPOPHWcFEPbk4pLJpttkZd5IcyYVAGuE9Ecl9QCTNAJSUXGCZySgGUZcXncbhqD2KcXCm3R8fJPtAjdbApmQyiEDDTIxy/Tph28pLp4rkbvczGXSZk3JwYk+F8CMS2exbyMhfO/+bbYTXI297nqe43tLbXsDgDznbaU1jVGnOSzNasc6Trx1O0pMI2ZWPCpAmn0Nc7chgESAlhiD0KWecpxtNyqJNzM9r7vx2QwAIndqGQTKuESCigY4K5ZkU84mTHSwKxkA5HyJeZmZT5AiCDMM/H6TjSXa+3MvSqtH/WPuj5OniFmW6aUX76cvv/blNES1A8wzFrOcjC8THl4XMVYdb/p8wnYPkM84gMqA5f7rJKiBOkukiJGvEuAc18y1jiBe5zwNUxusLwCyEEcZABSwLODOJcRuI1z35z9/My1Q2r5YZBdpmL7wFRU3GBccrwH8QGO7fG3CndrPXyaQYlREKXAAiCHxAxyGWts9nQeQOGDp+GhIqYFbt85Y6SKG4yb1M7jalkY7BJ5xzJsVXBiRXMIwptdNd+/dS3fAeg7G8gZml5DCiQDoszAAy/bw+d3n3FZjhSZv+bPaI8C/897j63ks+ZxyCADMUloHgLn+tYmz+o7rxGYFJNdMLI7/UhrFRKVgQh+K96qvzXGGKwcch/77f/e9ijPl1Ud34o2qAfSqiV5pAGUGoFHW+KIdRqG/xhlN3/CBUh4ccH0v2lAa5G7fwaJu9oaJt7/Ebef9lZtsM5CqEpIIINzAfXPpilIaT1xO/LoW/JpMrjek1kAuM8nV9RqtpFh3qg23RqQzkyK+oB5/BgBrN2cvwmAAcjI585VFWuuUfzsD74x8feCrmaaesE1/F2V6haZOIzIa1P54noMAYDyoD5i5fStA2AdZHCQwztYdZRARXGFj+v7PP0h/8Rd/kSbnCui6G2Vquz2ZH9y44VIWBQSDnrQaVmvR0HvTebp9+1a6/+AuAyYAddiIVtAu6YMhJGDRGoBrj5co9bJZhUt1DRgShIhSXPWVAcDQIvFGlrV81CAsJS4OuRaLRsYdAfUQWR3MxWCOgdmEwAqgAv5jqSz1WKTpJh0hZeXxHAB2mGlLKT333D0eZJ88eZIuL6HFgwA1BL1Bu0dGKxg8vS4y4wB6GkYPrjcLgwozlxiUjsd0YWX72aSiorIj+GHpHxyRwfCK0mGIGiPrCaAWmTy4CEKrRgxEiDRLq83zpBMuZgR1+gAwG4Yrtdsml5ofATpqvuA9GscqG1WAyPfx370oGQJjEcG3SvfQltQIXIEZdpSgpXjrFsZMYkkpAlm3rTeuUhy9ZIcaAJwvNL7w/HgBUDw7O5MOUrStAtBm3pGBGeMYIBjaEsEdA6+4fwMY1JCldiOyqCsy6jiPAiAFA1Cbs4SWWVaLfYfZ6qYk8eB6XmxX+1goHnc0X7CTZ7jJEXDOh9ImMPZ+4nI2jHu/bBxBLyMaAAAgAElEQVQCABD9i7HL+TpXhngQmqHQ7jQjigDgaMSAHSXcDIwGGKOjdHwGp8M+AWr0w5zAOtpAgRn6k+XJfZkLodQEpeUTlO7QHGQewKcYiN4jN3SxCzZ4oS+4mC1phgMgUaVwKvlxYsJMDK05RIn56NBA4nitSrCsSVX3zz4AkP1aS1gYgI7n9XVq6Q/vU+7jhgEY7oiedgaevb7HerGTiTWQ6d/nrLBYBmRvxLMTWDZTNhIhXCsxpwOg8YErRcmdSm4bZqbnQTZfivUIEip6HahYuAYAVEKoSRI2DLaqRwpXxfI3WJewnpkJvVpb0y1Kinb0eCsGXTEeWofQ6nNmLO7EH0VCtA0A6i79bHJpP1Sn0FSwkPlZxDxZ6zXfp9rq0EGnEZmP9S4zbNr/btqwPgG0456rwcbmKpRGKNssMzZ9QIsEdjb3CkmJ4mC2u0Ze/ZO6LLilBVh9tJZ4qK+cGa1VH5V9tg8AxHjlZw0A5vEeSYmtmH6odJBmqttBB9wTuMhutmnY36T7D+6nb3ztpfTg/gOWupG5v1oSsLqcScOrPwAggTEgLVYwiPH9domFpi/X4eUmvfGTN9I8GGLDgUpaKWVEzVu5y6OEHTHGO798j/ECGN7SxHpVWrM5noRb/ICJY+yrAHZgZnL/4S2u9xfn54qfYPZEprFaGCYmjCuR6Ooi3YADN8wtonJnKRONRns74k1Plb7mM+Is7kcRx+HeMO9Pb97i/oO4jpUPof0mJh76RUxqxFIEqFZragACQOBes5ny+wcjAGnrtOn1yJQ/uXlLiTPq+gkA1Cs0gWP91X6JfUtMRMSbeiD1s5Lg5culu4pBZ1GBgucngz2phLyzUqnsGP5rqN7YRilvT/Goz1XTcyX2JlMBN/NFmHVtBQyd3Tjicw766m8w/Vk9Ev0KpqVAGt2nz3fW8MWfWgNUQUHDCesIxs9V8hslyHFd7R0iAODFmJMJvrlMLFZKfDfuqlofcwwUlT5IXNoQjuO8ON8gvp6ymiDAhLgexgZMbihNFAxAS7Egga7xpsoVu3XvatRG5mulxOtsovZ//vbt9I1vfIPMOlREOKnqtQ/vQXIox2Cxnhjwy/F8nF8QJyhWicqRqIBwQo59Qrkaa0+jgkVrCc8jqzjXLdH/SPIHIxBSAmCydRQHoPQ6m7AhaRbSKPgezHeMNZhB4k/0c3+kCjEk1DTqVSIOiQC9tO5ljdMMrkX72gguNDDxCcXRzf6mhJeuRgAQ50uaA3XSdK7x2hsP09npWbp5RxI0aa11ELyCJrmk8blvT2QbVezAMt6YzBec53fu6fzaPwIDEcnsBdsBCX6O36rG8BBuhhHvM7tnPefzIYmLHNOrnbHfl/vYoX3p1wUA7gKH1XJ1zT93NQCr1c6Jn5i3rVhPvab5WGtox2XqnzfnssD3DADumEwUwQgu0gBRVSa+YrAZAPRjOEOyDwAkAFJ37B4AkBtNcQCtAcCyyQ6DgUbiPz8AqGu3n7+zbVghnIhewDNQ0ZTUcIB+DgCw7OBWkL0HAa8BwHpw/CYAQH5HaBrsBNB5FjcBANvJ7lwHzUL0wRLA88K5b05lpkiMp5IBKE0ZiQlnin4xscpJUuLZpbYZtB04DnsAwuTaCmr733z/p+mv//qv02KqkhIswALA5PYL8WECHstwgwNwQhMABU7j1Sbdv/+AAKCYRhLb3XRV/rjaSrPEGdJVoZmkhb+TLqHttRBg2O8DXImggXpi+vs+ABA/h+ZO+VLgIto82o2LOQMEUfVHKFUtsvh0N12vqH9I19uOygb6/S4DYLQVzQtYVixNN2nYLZhRRkBqUwkt1mFgEht8Hr9542sHhHBVxQvmHGhvfTfKYOXYm+Md3A8C5zATkROtNgsAXgLBVpmav1mCbYWgRQ6w+Ds12FBCAeaYAxMAdMjUBgPQZh0IeZVRVFvS5Q2BtOPbIhDTcwcgCJB1MKRbMdcLBPykFakEcRG6cCdHN9PtO7cpJo4X+k1aiWKQ5UCa7b673RoARODL+4vScQCAKM1GvwmoBtNMQYnLKQ8BgAJ4rUXoddYP7MBGGo3IZFMsOCKYwVDglZ1wefAoSn9353yZCGjEy2uQwAAgSnXK37GUj/pvZpDqsFFnXBn0Ux9Q4tzIouIzGA8YQ2AxohR48lSg2na1FOAdroUAsDmPwp0QB13pAh5z/AMA5NyI9cmaaU1pqcBZclbAOOkP2CcXFyjnnaTlfBp9DSAH606wHLcKwAxc5Tm7ShzHNheh4x9AwHBNLDUGpY3nDS3WhJqx3nFpVLuH1l6H7Zq9w6RvBy52WW2u0gAeHHtxvbyb5nU+AMqidNXrNg/OkWHfIfqbQWdJhXD68/ebdaDypuZe0FboP7KcyfDTwU3zc83+ICAM0XCAaywB1sE7A8LhAs2Ex6btmt3ELwacri4BzhWhocmUNfwMgLrdaw3e5kHlchpsYYj9a7wKIGmBUjFSyxirBoQdn9QAcA0A+hpmAhJabpUA6wZLANC33JrHdYKzOGjwwO19vtBALO+/DpDr+MXmPRksKxiBGiMBSOTx3QBXHLfXlFbl8V6ZpBSuJ5lB5P1KmlKRULsCcNtdM3d/UoKAXwQA9JXL63F8F8+/O5YaoCd1JWVBgC2AEa6LAQAiXsR8wr6L9RAHdTq0dxZkvG03Mx7YX3hwM33j9W+kL714n4DbdHKR3n333XROMy+I8p9o348KihoAHI5HvD5cbqHpNVtpbpQAINZZnHNo+pR6TKj84u132ARmeH/t9W+kk9PTtA1N3fliqkRNv58uLyeMRV944WF68Ut309HxMc2YDEggPoJmmsaPSlvhMgpNvNXsKUuKVwuZk3VWiiMZt7EUtyjZ7HbSchOlr163s7mV2nsKfUIAUAD8kNhLcpfHPoKf37xzO+LYUS6BxPp2DLkUxHaLc35/f3gs7a/+kAy1G3fvKr6LJH0NALqibLuM81OhaamqA+SepI/NcZTX4GC+W7poqP3TTGgwOxkHdsQI60ILGlIfcU4Do4tAJkowAVB0bzA+RWKVlQEdyNBsc9w9m13oOl0zJSOBEPsiKt605kW8FgCqt02YfWUAsNLk05xBXGqn4wZYpIEDk+UzaUEGMxXAGUtvmfxbct/GC+cWAoKebwEAijjge9wFAOes4pDWMPe6JFM0lHjjeoin6WIbFULoRwJoPCMg4avvL3xT1B8BYGN/w7oy6Enz7/6dO+mFF19Ig0GwnQtJsHK/MfPQ68p1ACDX+jAyFENUAJ/uRdVRPEeFpBIMgDC+kIDmz1eQV2kAwO3GWtc+f6kiKe8XIWYJrW8kKBBH/eiHP+JcZZXSUOPI0k3AL3jmiIqBzDR3kjbKskt8gd9VtI/HEffFTODQM8IEBM896Ap4BABIyZHhIJ2enqSbd2+Q6LANaakoLMol1jUwxO8IEPIqABAMQACL9+4/HxqAACCRPBQQagAQ554S5D0EANaEnLyvVBIo/rm1b7SmG6dqrl7vR34u/Ymzucbv52UA/rcEAGvwz/M+t8Wev1wLAP4vf/RHrdjYgbgHxD67al1UkwHIrICVeIUGoAdp1iaqTED89pLa2brZWgsmJoIXejPCyg5wIJ5vpbSztginAaf4d66lrgKpxi6+adX9AGBD2eZCuDOyfVBtZ4hqd+DdvouDTQSwORCuytxKYLQcEIfcdK4DAJsJF3+rXIDLz3Nj9OgptHN4rzWQm0vHGoaUNuJDTMGmRcpAPgMluRa1OSDxExkQqjrCpUjNypEnfq1/2YxNaIXp4AYAkP07lHkDNuCPP/oo/c0//jx9//vfT2l5wozz0eg4Dn4zfm401PhYr2Kh6WuhXq1VenKyXDFjfff+bZmMrKfSaIiGdemUqe4biuvKFdjtj3vB/UnjrGk36eJVB3QwRtBGLgFmFjeCGpQJxoQ3IITFnaULlRg/5z6o5qGBxnuGk2wfeny6DwAWeNF1KvR4XJIFVp2CQ23MCnK0SSqQkSaUSzp8OMGCzUAlSs6YYS0yst6scF0AJcxksyS4J4OF+B4fVPEdp2dn+aCC7we7C8EAxZb70iVczFV+CSCvFMaF/LSARwFfmwBeKDfNAC0YOrFhd0O0uDm3GiDT+EDmkAYzyFwjiCskGMjSCfHok+Ob6d7du+wXMCBkKtGMeYK4LpMIsLNcYzIAOGuPD4tLA5AzGEY3u2hzlZDgYKfsJQIpAMB2i8W+4Yy49wke7AmIgc4OzTuURwNAWzNAkFC1AGY55HHWHdjXmvXUgDHXvVojrljPOc4IajTXzG5sccAwQAbg2s+N+28MQgSsAQTEAXHJUixk4PtpcnGRHj95lCaTi7SeLTgeByG2TcZAkcUGEHkE4O9EIuOnp8cKXANg7QH8pSJ4HChCyxHfqfL0EecrQH+5VE8io635JMBKmot87pi31g7t9cQklJ7gMk0vz8MYRAcRCtlHhpvgoVXbDVDEWpElQZ4RAGyNvaJvvJ80Wjjtfve+Z4Zvva9146Dn95UADtfHfjiR5320ybBzu7CIuB2ki+QGxhfmEdodzGD2d69HbSvodmHOd/uhzRo6pavZhCXas8sJD2jbYLRa5wgMaI7XYDmUmnclMJUrFioN1+Y9MdeDAUhX9UK6IY+5cB01s74sxSXQskIpn8A+HOBLAHB/n+1nAEoLr+0wV8ZhJbO/fE4niB0v5N7PFRSxNznOKIAyAr1FQML1ziXA+YCuDzYaoW0GYB0g1yXABgDdZ7tufs2aa7CrjCcA4HCcuR+KRtX6EftDVXHidZ+gUSSIcjlSEV+VcWG9YF7FmGz3rQHuiDn3SAG5f/G5NlC4u06Xhy6x24NtUJX/sl0AOVkPM+LBFhtoKxd3vIcmST3EHKqegCYd3C2ZmNvOpaO2nRCY++qrD9Pvfut3eT55//330vlT7OmLNDo6FaMnmEZI0LEUNjSZoe2MJNTlbJXeeusXabbUGgCtNvcjAcAuXHGHBPChyfbmW+9of0tyf/+d73yXjBi4p2J/w1pAQ6peooEU/rxz+076yjcBlI3TqBulnjDVYjwzFAvflK0AvHrricCajdoA5gKMZXpD7p3TeYemTwAAUOp4OXsq9lNSoi9tpdmH2ISgyEAltNayypU0nj8BpEBzEImvUZz7AACy6uFYzMHUlWTKYrNNk8tJunXnHs3shkdjxak+wHfClTaJYQfgUGMqmDt0qXW5O9Ni7C8lxoO1FYxsx+VKFDbEDsamIQmSFhdab6OSJifEzZwaCeBECTHdiKNEE+sgnufoGK7Cq6x12KVZiUoo2W6xLyBiIWs8JElA9GCcvg1jnADsGdMWSQu57TZAeT2HSTmhdt08WG4yh8mFVhEnAfjjeuLlKOLqLlyRsQfF3CvPW9TAjDL5THygQ7YqFjCfXCVj8w8DUrkkOG7YmoW5L+M++h25YkOjDuP+xtlxevjgYXrw/B3GGg7zGqmRYH6aEVwkgQSo6Qsd2+Y/bVYWUiYqEUdlmvqLcimUmAlG6lpOwGAA4hpg+DHRutK5jIl8MIGnl8EeFBOw/b24mZWYvrN5+vGPf5yZiGYAWkpJWoAAIGOfBtRanFu4thQJpawx5zjQiaakqia1RaONDQCfBIOu5tMMwD7Of/1uOjk+SXfu3iLjcrPW8wzyuGni033xx+7+2B6hlwtp/b3wpRe1jp3CHXyZ97VhrP0el17b9wGALTwhvsbn0DK+b8UPO/t9RdA64L7bPEUQL3YyxA1xxut++eS72n7tdvE6WrZfu331/usYgDXOVfdHVYDbuom9fWdtac/bGgDEAqEbtYuNBkijAWCBzaCgu8Qu18QVh1BmkKM8pSoF9p3Wtd35pisA0BPCAIXvpynGi4FcZVIzorwHAPQBlRvENQDgTuDtBgxNKy9KBwHAaPjMADwgdt304GEAsNXLRUaazxEDeR8yzUWjqqKpS4B97fy2zwEAagVtl+L4AJC/z26JzwAAakFuXIBbgW1+oNiQMyDZaDXxIOwDYC7laib+VQDgAmKmAPyog9IlAIh2/PjjTxgg/t2Pfpnefvvt1N/eYIBy8/RGlO0pczcYxIayDofZAgDEdW5DQPW559Ktu2cssV2FBoWZcCgx0EE+AOS1AjFR+zfsb20s4SRq4DAApzozlwdABHgLZsDUdgiEywNOC7iIAK4M95lBvgw7+W6HzDUAgLqYSssAUjCzPBiQsXQ+mWh4xP0ehRvYDnMgKQDLmcSCYVH2VycykKaO+6DNktmNyqkZMLk8Mg5iAHZUdgEdvSMFoyj/2WxZ1gmwj2UYYCaQdbBV2aQBo8hs25UvH/4CEMS+I22VplSDGowrMSGbkr/G1AXtYm04dDc2UuwQ6F8Dxh4HR0c36BqNwJhgpcsVWW7dCCcbVK03IpQuIQC6mMrVmixZXCNKhQAEsjQ0mIASGCehURtXR4GlATseJpBpD/c4ArdgQA2RxV6QGafvUckPmFHQgFlP5zLTWAnAUpYVB5arAUBn7rIrIA4mhZGD7hFsWJWRW4zcaz2AAoPW/FkEyHhugcsGasC887M32k0uBeusO+liMklPnnxKTcBtlLejlAqv9XIRpeEqpcI4dDkwxipL5cG+G4WTdRcl6IMEIBBZ8MxgwnujVNxi1lhvJk/POU6gpeMMNdepnsAqlFpz/WCpDBhpozDbkT4kGDJgpKDUnSY3wWijKDiMXzKorKATP3egyjF1DQCY9/mqBN9ry3UAYP78gX3tKgBQA7UBUhW8N4A3/70RQ1PgXsP29PfCYfT4+ISulizBotFSEy0CqMEYQwk4mX8zab/OZ5c89KNfPOZKRrnXW2so1+5shwDAZu8PIC4AQIqKt8zF4p1lCTHmOF0riyAgAEC8G/eNRIcSOGIA+1XGP2WslKU8AgD0s+Z+KxiI+wJgi+h/HgBQG0mzTrA/af7S3LcD6EYjtP1Mu0FyMPgKDcAMYFX7ow4nTY/s+1sJANZtYwCQjxFAva9RAoCx4ubLmwHIz1UMyLqNa1DyOlCwDV42D5eZyXEXDQj4+QFAAuy5JE4XzgSEGLcGSPGc2INGAyVf8W/MS5un9Do6sAMAxPr34oOb6aWXXkpf+fKrGQDE+B4fn2UAkMsDzQ96aRGSLOPjI8ZxANDefvutNF1o3QQAaNOsEgBM6x7Z1G+/8y7vH58DIxEAIIDIflSK8P7pPh/aYp1EdtW3f+8lMYphZrBZM6HE1zpAWVQ2gFEUiaT+Zsr7GHYRj/bSahEs747irs1GFQEAHLTfqvxxtVapMDTAWPKKJMV0lp7OtE9vCykVxpN0UkZyFmzIGTXwkOw77iv+ROUW95UB2JEDAp/YbxYATVD50AEwqH2NJZE0yZJrMPp92FdFwdHxDQGJkSyuC8KgWeh1uymLjXvGm4O93OkoAWsZFDCQMNZ7m6nmCBhJoRGncaYz32Kjip3Vpq9EZAdxIa6lRF+vH/Imy5lKoeMGRzTe6iYwRpk8AaCExGDslzjnabxELBbnId1DswaBcWcA0O7zZHL6PQE6IXGqDow4x+fcYDDxxI4EpRP4AG6DUY1EuDQosVoXJaROKEdSG/PL+5jXqnKftpY22e8DMUTNDCzXPo67MKvcLrWGAABE0hEJsYcvPEyvvvyiSlJtKpg7Ps5lRTIpr4mfAwBcx/mcJJIwuhIYGGYhUQIN7UUaoywAkMv8A/Nrcv4kg9MGAFVOrEC4P9DzLaaz9MYbb2RTw/5w3CJIYFxxflrTlXEWtMSbpIsBS74vHtqSIf53JmZldp4SttCClHkiWLfdFgAIJu5zz91hZc96NeHzYJzwzx2GoVt7d133uC33GACAGFdfevklJmRGZ2MmxMFMZbXE5wQAaza6AcDynMh5nCUh2niJn8JJ3HJ8amy3iVmflwGINjkE7O0F4GqsqfJqKDzdYrx42u8glLGe10/WjgkU+xefrYhZnT/9t9IA9ILoiV+XcJSZY18QfxrwsvlHvp1sZx0AoP+94wLc7gh/3geDGrAqm0EBn0oSM0PAWi91e+UHD+0Pl9yFuH5eZKr7qzu36SANuHVHDC8Djb0Q/aztmbNGh23AcwlqXZJcD+R9WHnZ6e2JunO/B2rDfQUvBLndo2gym3XEL3I/1NqIRabewDE/cgAArIdrCQiWv6ufelfzcRe552Q8gOTn/o129/MMTKSoNDx8sIEYLJl54bA7OoGYai+98eY76Qc/+Kf0//34E5WwLlWKATFnbsQ9AYaYNyyVWGKhBAttyGv1NxL3HG8u0u/+7u9ycYZ2yrYrTRq42CJDhqCL4wzafGBcLYJ5i+AGTJ4A8Fzy6WAiMy5Ac8dGjQwoAZoRgyRoPbGbgtbeCfHYo5NTmnkAqNE8l5uUnK2QEdRzITBD4AYRbGpPDFDacskSAgc5pvQLgJVZBFyAWXJgF/HoDzsW49nlVoasr5xim/EUIK/LIci4bWdQ7a5lNzVLF4B5yPaDsQW0dAIARNsgUMdLJSsK6OnOyyxuyplnvAcgIA4I1uQZ9BpGEfoVLU1gkW7AjXirQSWw9XCAoS5NaLwoIw+3WekHsQ9ZahqltxApRk4SzDmMnX6fZcvI6E2mcot22Tfu0SWruWymKKcu5xg22cvJIgebYsjpAA0QCu0yPpZmEsw52oBAUwoj4E4ahABKvZ94L8F9zwiIrHgQoMj00VAA4EIAoEs0DDQCkMpztjgVNMBfs2Lte59/hnHq+2D20YycCLIpc1Ro7eC91EKipmMY5MT3200X77Fo9/QSIPU6zRcTlWiR2bhKKCHXwUMHSKxLdASmezVARrXTsBfv60B0fJj6MAc5GqfR+ExAnU1K6JTXuBXb7RolLATyoFcJAKoAqLg/x8GKe2RRjgumo3UeUbIF0Jvi1SgH7g/4JwP5WE+powNAN8BQl9WMBhqXKO0nI2Mh6QKsXSzxjhKl0kykZPwwCAUDLVwUOZYYoOuA5vJUtWPjou59bh3rbsPcsvmMbnzTCyaxtbsIbIERoIPQYHDMBMzDh/dYsreBSDzA7IJNqwNN4+bLA7IPEGvNC6wZGMc4COBwPZ1eKCMfgeaOdmIMUD9HLpWsA7QsNaAP7JhpxPsNAHqOZmA17jszcOJ7HfDiYGZgspQPKJlb+t48o8olJA3MDK8C6txetQt91iKMyxQZaQflZfBfspr3BdqlVqSBsb0gV1UC3YB6DpDbcZQT4c2hJ/Q1K8BvHxOglHQoG0tsvmYclc+T19ZoH8RF+H1dcVEm4rnOFgBaeRhBv+J1SDy91YnFP2oA0H1yiPFQJg95P7FeZIZrxcSWS3bTiJ0UDLCiXfX7qAToS4vMa7jmHvZOMQNRqoifdXvSqesmMdyOhx2K37/25S9xrwRDB/GVD4SY41hfvV7afIuaxf0+gTxqB86lodbtHHFfdeLHw3o0GtPI7KPHj7ne4f3YT771ne/w+mAUSQsPJXHSDOU6jXs8OUn/+puvMvbbJuwjG5pUsGQT8RcP9tGPAQRhvef+Ay1qlPh62AbBoRt/NnFUyK/ABbXQW7cZyKp/yrgMLst8zri+iRuPH3/M9WwxnfDP+dOnbO9hAEmdIUqDVwRIwSrEeMT+uVzO+ecKABwXc8dxqibA/mOnVMaHoeFnAM0auTdv3VRieQSAsR9urgAnBeAMe2IYIlHOuBMlvmDgx7q0hY5bcT7TGAUDVGWvy62A3Zxg30qKoAQ7cH8A/rhf2OzJ2sZFZZoqNMIM0ASaAFoAbHP+B6OPMR7i3Or86YS+7lNl77x/L5e1uaM1Sn3uDaDL5s/1PK8lkDpzxdl4gcEFwPfx48eaK0gYcW5BgxjSPQAmQ7oDAB+enUU10pAkQBuVSb2QtuislUgeHsHAZJ66/VX62le/mh68+DzHy+1jyL4s02h8ynl3sVgzLll3AMgi5hDQivM1zwjQqKUmnJ5sEaYp7EOar8gkxmcQjBOSI6AHyjkU7DleG+zAYG5C448a1FN+L+IfxGkgLfB6KCPmdV0SHCzkMeY/JD9WBADxLGRcGvANpp8ZqtaGBDNQY8CmcwH4F4nn9r6r58U5yi+ue6EhjHML1ickNHH/qxgxnVGf8dzDF+7JPbwHMsMy9WgUJ01Bra37E+4iVpSnccWhBnifzpbsr+HRML308supN+imo6MxmZN4DWNDUAlwk1Qvga4yYVeP1xwLVLhDjhWugUeuY+pZU7b83lb8cU2Crza7PVQSfIip15yp9j9IZsN7PFX3Y21Ex0FeN3YrTOP6dTt+EQAQX+YN47cJAGpDjExDpRWUg41nBADrAVMGipqAHioNAKiJqJ9fBQDycGMX2LgOFsjWKzqoCeD3I7/NZw4xZdqXLQ/vmvC6gZKxyEWzej6/7yoAsA0MhElDfOC6Ae7SnNr8A62rwO7/J+9NeCTNriuxF3tELlWVVdVVXb031RSXGUoaayjJ8Agw/Ac18HgE/w3bgAegPTDs8dgzXEaUTA4XNZdusvfaMzMy9jDOOfe8730vIqq6mk3JgIMgujIz4otvecu9555zbpjnRoJYX3eW0MQf9gGA5fmVknZc24sAgPiKweSIgdKPf/aL9Hd/+7fpx+8h0VunPkyhu700DoYhvGkMAOrUwvR1FVLH2Zpdxl6/NUmvvvoqgxxIJwAA4rXMzB2ALwDPolFDYfrO8Rjmst4Am/sTQPJKTQQs2Uf3N7xsTst/Y3MLivrkGBVySTLZGKIDwEmbio4jE2xXZse9vgCtzSY9fPiIFS0EDNhsILnFv1l9c3OMCJhWNPVep21Q7t0V3E0/BP41IBDsr7OMrkwgsNfaBL/wfPJ4snQBnDIBCwbVtGFyDtDnUKa5g8GQAJ8rbfLgsYeKNk95AW7Vdau7zBJnBoodMdwI2UUDFCdmZhLSXJjkJASbAfCuFYCZbQYvPQOAfEaUBMPjRwAVfD0AGM6iiYcDGz0fV/+D1ddeCvJPkjYrweFcMxuZWwMAACAASURBVADIf+v+DFCl5YLRbFBKYp2IBNNtJVlR4+EtaTmk3q648tzYzQ5gUSTD2/BKrKRlkODgfJz8HWac7G6cDFqz91RTYOH1Fc2kOH+26nTs60FgwYAmfN7ckdfyYXsUki27Wafx6ES+i/CggnfUpczcAQTxtRFQnwP/rbv+grnRTb2tGMI4L459ANOQV42O1UxoNCTgCyaJ7lswlAiQrxPmn8HyBYJPsM7wfwCBwThEoIs1hN6E9nqKBAzHBnB1fnFOiQ6ajBC4CmYCHjPOA+eGOclmOAjEo3kIumh632Ait0EHbLFe8TnOleJlg+/SDwzvRRdzJqBZ+iPPTlwT1xskZgjgw8uHgDq9C20VIL/K1VrApf0c1111qwRTgUDpUgy3k5OzdHJ8nN555+sMkAEgMJGEtI4S6KbQsm/6IKFXYqD/GgAE0CAAEJV23McIemvvxDhoCYYwQfwCAKASnwDe47+fFwDE97F7NAoMbLzUyOzK624krO14w4l2s6jouXDJoDTR749EtoqQS+bjTnU/pFs+9u8DAGyucRcALK8/M1o+BwCoa9/1mq4BQN8jrUNGzgR8uTCau4xWzzV/piOGVP36sgBAH9cMQD+DvOcW6gGdU2wXe6SN/GwAnE1cLs+qVorJ8aP7wDUlWBb+N5UPsJTo99Lp8Ul84YprPrwAMc/BkAMj5fXX7qbbt2/TmxjFO78IIsZ6iPnugiRduvoDegD++r1fkwHIomdnrK6+wRbHKeK8+j15pZ3PAAhs0mwllvCrb7wuBhoLL+pazHhzOA4lyYDrzxsvHfE8sf6waVoH8w//jo7uS3iKdVM/uoiiSQgbGMBkH76duA+YY1s1XIMk2XENrjXHUGFO7ntqAPBqO5bnZzSm6ObmCogJEMupI/oA8msUk+Zz7m8Xj59QanwxV+wHoAbHBhMQVhjHsLYgwDILgC1ingBg6HIRjeE4blyAD+Ci25GXM/YyjxuBOvYEDNBiFUDKQN6sAJqw70wGAo4g1UVsCQDJRTDGYVv5JPdGZxwS8JImA4yF+aYLvMd5N+LG/lrXwcJhAdAD2GETs5DeGmhzkwr8rH2smam02KgT8lD0NPMrCq0liaeY71bE5SZZIYHdBwAyZsnMZi1kk8gH9AxlTYRYAM+YSgPuyd28jqPJW/m8FknzLivP1jruYCtGproAbwlsI67q9tbpn3/7n6fJySg9ePAgXT74LZ/z7TuvsBDXPzrR/j08UsF7LoUS9lHGaasALF2gCzM7KVFkkYOYsVxPWXyMpn8aQ9i3tb8TsIO0HAV7/g3x/DJbJCEmIrkiwMIaADyeHMl7e7mgB+AVbIKCic84aqCCPucnPC0jAW32vXb8Wlp6SZHVXtk9XlxQLT2EuX9uVeDMSsOBAPmX791mfoGu3WTwvgAAyOedQUDv7bIeuEA8Bcn40Si99tprqTdU7odCuPJrXV8JAPIXFdHqEAh4CADU3glsYGfra/3iiwCA5QEa79LqOcQcrIlJvwsA2MZR9OB/XwBgxrWeBwDqRks6Ur7yyWZPIAegUa2LO1EzyXKXjDgYPAr2vSxFLB+gvBrqYKcJtMrAqdGMh6S5kNzy3P3zgW6/Pqfy/PcFoKtuMGPiPgzCQ6IM5LlRVAyzHKAfAAD9/ZaC7LtHBBii0vusabAvQPRz9cKdN5wXBADtcdDcrwYA3P+97XF0CAA0oJyDBHtoVEyEesLVAGA2G64C6IZxoTP3wuvnlBkbUYmH6S03tskJ2Tbf/7u/S9/7/vfShw80SYeoOsKkP4J5NIdhso7KKyj2g5GaX8zmYnltu+nWrVvpzZdOVXnrSK5qU2FsvGLQAbhaUZql+xnjOTqKmdHk++8KvhlGYPCxG647xG4laUUlj54n6KtLRpKAPAARSAQh0STLqauKnOSg8vejJ8qmkx49fqzAil3SBgwcuLYHOCAPwmDihPTMFXhIq2XqrU3YICLlwuGrosBf3ZN5XAI1YELK3wOvYSWBx8ZSBk+oyJbSkdwVMgCoxUyVarwP4MNoJONqd7l18xUE1QYKWWELIHC9VMIBIAABaKbUozscKqIdVfK9kalpCiQZITUNg2YEGbzPwTrrDZXY2QLIzEZInsEUO5rAQ27ArrF1MubNkeM6mns08zMquH4uazXeKOevj1cCKYhh1BUwuvaGJ6AZn2KV4pk2ptc45jwCeDD/BNqE/JuVWQTmuJcyTpZXksAqAFlgIZbPWvewStSzt2h4q8R1ea22BM/3BIAEryMzlwUAZilxAICWUbs7H59tSMQZOK4EtoGpQNZI1HFQ+Xz48AE7JUISDHNmMgpR7SeA1Xhc4pxQOGBiYi8hS3oARmO+DQeSDE/GYuYN1IXbzT4I1BXyWpyTPEFRiQ7Dcwb3TbW7nE/olq2mLHNKvRZTeAMueJflxdRerw3SYL7gHoz6PUreIHXGMQwWLpbRdfE5HixMSG0PwkRjwAQa5vSYZ77/9XP38/T4xnwlQGBPvBygYX/epO6gn05OTtNLd18mIIAmLJiXo4G6cgLAtfeqEzUF/pJj5fsQXTEBGbCggPu8WrKLOQAGAL+QhKPJDZ8v39+Ac7VXZRNTqElNvX9lwKVirmfA2hLgSlqWpePuNhwHzvMiwqiy6zMZF5HolmtoGfc4AfR60eM6V2QpZhSYQVrUN53Il7HKPgCw/D51xI49eq+aIZgTXs/QpGmfLndPE5R2PPdsANBSvjoGzPFTnajlORnHrbsiuwBbdUd0l3Qn9HUCs6uIaTNem7jOTY+epyApn8bhfwMA3Bf/1h3iSwDQ58K9O/sANvsHv22rguS+RK1pECMlpWNBJvhsUNZNZyfXAiCKfXAbscT6igWEW2fX0sv3Xk43b1xjoo84qGHjN8xCNjUA8AMVx2hEAPBXv/4VPQDNAGQx2nYRvWBowZsVRcPoirpOQ3oIH59el9dW+NmJsdjPnqQAAgAMThcP0927d9Orr9xJN9FwYzXlenIMK4pOl16j3Dc2kphutrBcgV0JCklgSYXpfuxJ6/BszR7Ifd1vN2fMTOFYl3tdNUWBtx3jLadXMV+Oj45Y4MILcSqYcI8ePUzTJ+eKd4ZHLAghhMA+gB3tgw8+TG++8ZoYUwnWHgC/tP/AEuECthMXTyPeVZM4+kwTgGsaHJTjx4UfSEjLvAKWPBxGeX2UxNhSXXQZRmwBj0UWpcejOG953C2gzIGiBNoNFIBiCmDvxNpDmSrGL+JVKt52AUCOmwCgACQqbjNTrln/Gb/W+doeCw3H+bz+Kt2tmyQs4u8lAFh+DoUsxtyIMQprL8+3IaXbUPZo/4b0G/v/JZUulk2jMBZNNEI6jfyBzFE3ZwlAzQQYFOw47pYG8d0VZJb+/M/+LN25d5sx0vTTXzIvGkxOGc+ut0c87pvvfCPdOLuRMPQAdmW8KCw8fL1sshKkCzwfArDFfgtgLnsBhr80Y8qVyADIHzH+AHyyEIAuy/THjML8At5/unb+H96AeF9Ii0dRmMdnfvzjH6d5FH7V9AO+zSpwgBnLOMLNh0IibQVhaWXBeDDWy911scJhNvYW9Iau/QAMSgBz274UTnfuysoEACDjIcSGJQPwAI/IHsU1AGhCy+VKhWUDgJ1+h+sEmhTRszUee+MBGPM15lnp98r7VDNcK2JATRTa6TVQbWH/WABgjtucj1TzeB82ot+1n69VXJlRWsQZXI+qLsA1HmNmYMaxKgXGXgmwVrx2RIMNpA4AeMIHAMDcHa0iuAHoaJn8viAASElfa5C0K61sSkKqtW+kK/A+kRiAGYF7XoAUlevMKGnfl+cBgHkif0EAsAZe6xCtBAAPAX3PCvF2ALMvEQDkhN4J2p8PAKJitw3GWJ5I8bxqKdIhALD5Xn1fA0TpbrwIAMgNHdJOeJhMTiiX+/c/+EH63ne/m54uT3hsWEEgYAu+FJlhJQCIbpEM4FbqXnf39t300ku309lIXm9g/FGaFk10aCrMrlECSFZLGdj2LGkk1V0VJcm1Kil4ADWDoRJ5S2BQqaX5rgGQTDkXAJgrir1EE2dLN2B4LVlJV4HBKqWPP/6YniFoaHDjxpk2FviW4bzYeSuAOwQCXYF2uAZswBmIC68/MYsAPAZgEv58OE9v4l4MTeVH4MbKUiGr4h0jQBgBfTQfMvOppJ7jc1eXUwaukOTg2twkAYACgbqQhBgA1FiCPyFkrVcJACAZT1EpNQDYiUo11iF30WWiw+52kgIJaNB6SNCVJuE4h36anBzJAy6kNmg6QUYmZKSDoUCh4SDNgk3oOW6gogTxFEg3rBzPB76XlcnwjwxTffzs4LthRurcuH4b/MO4Y8VabDSBdf4ufR+AKLzEjGwAQCQoNDNnhT7kbps1gW53iTZ7tH1t7XFuj0xLZ8r1RuOi2YA45zFmwktPF+l9rWQ4GlQQK4NSM0hz0Zwl1hKPAQRCYroGAz3G8/3799Onn36aZlMBYWCgEMwKiQrGC72cOpKugfUHIG/pZkrBwMN6NxqO6KVEKc1wyPGKc6FcOvZCSzbJFma3aAF0lu2yo/h6pcYh06nGNuXiAvrJVkZ3watFml5N0zqYpQTVMrvM4wKMMc0BejtF12MClH1JfWBajnGKZ6n1NpiLe0q2OLdmXRDAD6CbEmiDrSt5aJGZCSkejdHVgU4Ae1gPeJ+Ic752dswE++69l2nK78Ii+l3iheejxDO8f8Lk3gwB3F8yBhxzoHAQDCX8frmY09sILEp2VoYE+OIioesn2aRFEYTHsJfTju/w8wFAJfKeDfEsXgAALOO3xqS6KQjI46jZL70OeC3g+mVJU5zGPgCwzQDUG/N3Z2sYX0gzny0b9f6Mz5QM0v3xTeU1XDcf8+16DgBYSmg4x4t1QwNY68jvGwCkxxcVJcHgr5qH7CQ0BxiAjbekb4D+WwP67b8e/skAoD2pOBYKa4lmnVDzg3q/yc80EwOC4RAR06FEzeuGpXIGW0DBwp58NBgJwIH2kIC79pPNasr7OBx0yCx65eU78npDcYNdbhGPuQO81jPuX2BFHU3YBOQXv/gFGYCHAEB8H5hJWI+uyJ5dp+7whOvf0cmpCqM9dMqFzYasR8wEQ6GD42kw4/eiOQelrh00Mxmka2MVAJHgYY07PT6i5cdoHB1hu5Alo5B7lAuctvDQczYRQ0Wg3GwBzYJQdMNXYt1bhTe17UZiCDhh9D6BX6OLKICDjz7+KM0vpuns7CyNJqcqOC23lDJPp4v03q/fS2+9/Sbfj+fCQkpHTT/wHAmkBJC3KgoGiNkWizUBQthSgH2F2Ij7GRmWW8YR5TqxssdbAQDyuQSgC2kzrn9IOxcAWbFuxl7l+NnSVlhw4Dqwx9Kz0QymOF7fAFRIgF1w57FRqI84HV2t8b0ABAmAutCVJZcBjBE63veKeK0CDjB9ynUwSz1tqWPmvverKNA7n8/disPqqbtS11944AEI834HRr5BL1qcLGV1Yia4Jdv4PV/xPbI22iZ0peM6lgbcq0fjAfOm5eo8ff3rX09//Kff4vP98O//hnH38EjzZTrvsXD6zjf/mBL+49MbPDw8jdUNW4U1WO3Q33uojEu5xYZAZSnhVmyLmKgBM/nzQpJ37PPa5xQHLlco5EHNoeeyWUJ5FfLyAADVzVeWSgLUZMPyox/9KC3ni2CGKk8amAFIj0qAwaGYiURkleL+BWDqkZAxkgqHqbsEA1BsmpPgGqKg0lVBtDOAkkcAIMb1OOASz4scHzMO3kUBnwUA4lyna8V/lAC//nrqDtRAZjm/5LpYA4Bc8wBMep35HEzA1p51wILs0M71PABwBz+o7sGXzQA8hNHUeIWv5xAA6HWnVkAeAgA9vpt8J/LmfQxADvwC5xLVeRcA5MQrOkzuQzAbz49IsGoT6ipgqwGeXMk2Ip5NHz2EvFC2j79TOUnh55S7xkQAUhMK60Cxkr7ViLP6iWkB5MJkoHCnC27TDY73Le7vNppD7CDbxUa8b9Dk+/I8hXAxMzI7rJRQVibaOK7AsTbweVACnFH7pnmMv5LnfSggzyuda25toC53mcz3oUKSDzUJ2KkgNIm9Aut2EB9eyzvMIo8fBEKscLIz1jCl4YSJ/f/5g/+UfvjDv01pA98oeFMo0MLyi/9ig+IiHkAcpJ5IWIfhtfetb/4TetN0NhdMuLkwkqIfHiJRVczAT1SvwGgyiw6fgecaA9qCCSdTe1V4+n3JOSVNu0ybFTp9YheIedOVvx/yDTCc0AWPptjLGUEGnzeux9I/BN6j/jgB5MDeicQf1TrcX1ZVF0sGDNqIzaAJBmPtscmOxgI5+Xyi6QauzUwwyRklB7cEkvIagDMBAJIdDPAjKnd+6ujYyfkWyY+l0tjCLQ05PkEVXJJVj/Netx9dhAdZAizZqirqBgBXCzE93bXYgVCWXCOxKEAPLNAOUAikBcOUPmqWnw6G6fjaqViXqIoiMGZlVs0+kMjsAwBLsKaZg97YGwBQgV10ScuSBC1I7FsSQa9AIXWXdnMMMEsFAIYcfIvnBDBLfjH5Pgbw4wC6SUgaea7GL4BisTK4XsT8pZE4peqSIuXlovAWZQLjQkElnXGlnaBlyQqkJCv8eKIpyX5mS0jgGXwqqIE8nHKpotEKAiRKlVEi2G6ZoGGdgMQenXrf++V7BIQW4YcHxqolKgiQukt4a/bIfCOrL5raGKiCtx4TOwTJ8NyEvOnoqJDYNxu1E0DMJXSflDQZLLoBu1njBWCOQFX2C9TfwWSU5+hSEtbpFRMaJK9IAgejSUJDjNu35CXz8NGj9NGHH6anTy94/Ufjkbz8QvILYBDnkWXsuau51kczru1hguGi+S3WIRJUdtWM5hJg5p7duJFuv3SX/wWTA8e5OL9M5+cX6cnj8zRfzNOoP0pnN2+mW7duEPDr0FMVkq+Q7BbeTAjQxz34VS3TYnkl70YmCvJsVEAsQKPxM5Vk1oW57fKK9xmSKTB/cF+RwOC/+F4wALn+sPrfMAH3sdTaDMA2YG/tWB3QghHCeVBJgC0d1VwFs7CJ3/i7mFC2BWMCW7Ht9gGAmYEen8e+kOdmAKPayyK+yl2P24VTJzie2fZK5LkVBR2si+V98/qQ17c6aakl1G76tQcA9HkSVNpzHI1Tf1PRzTaAWAISDjsrr8acTMV+l59bxVh3N8bmJjoOapqY5ftbeQIqaLBPafEcuK94/OQjt/7xokAgAEBKTePZek21dUez34hhoveV4G6wWmoGQlKi+jwA0Htblr7BFgFxCJomodN3cLd6sDbA/Fy5S+6McRYYdmiatV7KrysD8yh+sKO91gfMI6xvl/NV+uUvf5mWKxXIuj1Jd9mfvrgXR2PIVFfps8ditHUHaEyS0t2XXyFTf851tHmWgEQYH8Zegv2VY34j9vRRV/N01FlJykwVBNZ+FUa3/QCGu2De9FJ3oXgQsRquc3ys9/UH8nLd9oOxFHkKmEGaYzFuwqMVEaGlkSze4n4AHEGBKp47lSrrDb0RLx49STfPztLx6S0xx9ZdApT3Hz5OH3/8Ubpz53a698orqdfTPgqGNV4ohJXrw7Ij1jH2REl05ZFLuSiKVnNJnc102awXXF+fPHrENXc7CA9AM9RQKKVEWsqbYQfPj0FK/t7SC7icjiw89fsELgdDFP66aYJOz7xfW95vE0vy+YSUHOsEvY4XsefB8w8WL9jXQmrOfSD2ADVDU0EV13fI+sH5ZN3syHtKKaP2ffX6vo9N5XXO8xfrF+cLfaQ1TkvLEEv/AYTwM4yBmzjcjFMBCzAeiELcKgp5WzQKvOI++PDhw3R++TC9+uor6dvf/mcErhb336ffoMZlN606UFRs0jf/5NsJ7NNPPnvIc7p27VRNLLBPs9mFYsNuxDXOFaCIwT4OANMvPFd4Uvqaud4DTATTb76MBoBiFIO5zzwpPLrR5JBWRQGYbzaIh2SRhBd6GOI8MAfAAFwvlAcifkGcBQagAHiNo40Ze7Fv7ir82iAcvBc1cNv7QrP+hkWCC59J8xv3E6/uUOvjrdvXCajCMoXjfxkFE8RXOyBjs1WgKVHtAVhuJNOQ/o+ORwQAIQGmt/dMzZjghs55X/B+WvtC8d3PZf+VX/w5//08APDQYXIBq+V/2LzbWEqDH+hvNUCfc4s9zMbyu3cxniDPZAuVYEa7uVB8uLyvnLtNoFGRryL+K/A3AsQlANg6oeLBGAD0AuPFR6tF09XoEACozzUeJ7xRBqG+IADoc80URyeJhSSsldhtNTGyxj4SyToQb4CnZsKVfkW7XeeCPROXdwgAzN1q4r6+CADYXGsT5H3ZAGAegJVpt+/hswBAjocwNS2lWi8KAJaTwM+h8fOqJSjthTIn+TuAaJtxWCP+zwMAIe2jRKwvX7DpakNvmP/4tz9K777796mzvdZQqhm42fMrWCM9bfRghOAFABA//9E//RYrjIOeknEukqhQb1WZouEuNsgVkvgNpbb7JJDY55w0aZ7FfQlpgSW8aEogll0jAcb7IYHABokEGwGcpaf3738qbyhLvCJTpMwZIMToiMCGuhGj8528EQk2AjSICrQBnuVqIdPn0VBmzlys1mk8BMCmZh8CpgTEmTnke1OCOJm9SalHs2qZeYDvAXBmk+lW85FgJ6JSiNdkKEBF0kElAJTjRmBBDLtIrlHJx31BUIAueqhIIjjHtGGH0JB4YsMmw6CidJcAIOfNGkxRVPEkQcaxyPI6PpaUehzAzRrymUWahx8h/W5gLl6w9bxOiRFVT4R6vmh8IS42e4xAQpg4c3MgeKDxC4Yb35clwNF8JYJaB5ZoZmI5MAN3e9aBAVF4QHkPsZzagSxEMngfx2pcGwIsBvVheeBkkIGcvY2eAQC2NvkIYL2uYX77WtvrjzwjzXiRbEzBDO47kiEC9qvwquoBbBcTRVLyMefJxx98woLBo/sPmCAMwCiFpw08FgGYzdUVG1J+JkA9seg68KUL6U4NAAqMjApvPA8fz96c8KIBMNUk4wD+e6kfAQXuL7tabzX/7XFW7odkWnQ79Kq6fffldPPsJoM7XD+Oi0Ts//jf/x29fOD5ggICUnFaBXS2aXopGQjneiQ6WaoViTBKHgRAO/KuwXfi+s5u3aZU98bNMyUWXZ0/gEoyfuK4SBjZVIjNkFDg0XPC/sH1JKmZUqenQJkeQWTc6LwwfBCoz2bnvA+SqvcyEwKM61ICbDZHDtxXsABYkrGC/+NYKKiYuZYTuEreUilGeS7PAwANlJTj1JKwpguwhpOZGnn85uZeZqrG+yJPehYAWMZRdbdiA4CNDL1ItJn4NFfaisdiUhqorAFAz1klW4UscKdrXsVaeEEA0N+zDwD0fSylc8379a99AGDJfis9h/WQ24XMFwEAeR/qZO0fEAA0mMzlyoBnsc+IEdgAgHpfAxrW6z//HoX5Q4laOWYUH6gAheZGWG8B4NOag95d8Cl2ZV2MHzTpwlr5yssBSHXEHLJ1SfZqM0DGxkCDhO6W6AI8m0vR0OmOuI4gPrCXKs5/NNRe9d4HHzMeSP0jntdXv/YNrmMXT89zUy6uS0mSYo83FNaQmPeDCdaZq3gAAJB7Tshv7Wm66SuuQJzIvWgz4j6FY5Nh3vfeL6/pTl8AI6YFQcQJmocdpclYBUY08eC6GfMM85DxUzSfshUPGzqhYJU6km4+fsomJpOjG1rrtipKPnj0hH+/fuM0feUrX0kXl4/DWiG6i0dBxUWwVVeKLbKFcD5JrDs/6/FgwPUVgA1e6obcTZulCuqrADpcKHLzEifin33wiawtolmarTPmC9leIE7Dei/1B7oWoxiGBmWNRQsLoPx7L4Hoz3kQRALEbTyfiP9mwQRFIU/+5So4lrEZx33uwhRxWjEBHI/oemN9q9a1Mh72veLyEu/Hp1y0KoEVjLty/8hNPAlyKZfNRb8BJLny0EUB0l1/ca88L42z2Zsx579rSVNhgUJrmKXu90cfv0fm6D/9luZH9+LT9MnHn6Q0GPHn6VLr41f/yR9TdfHpg4cEegHKYp7MrrTPYhwgX7r10r1sDcT1OkWhDecYxR969C2CaUtiwZrAMgubWzUhgfSX0ufpBeMiPFN852olgoLjixIAZKzSg/xdnbV/9tOfkeDPwvlW7HV8nucV1mAAALUPyPqk7J2gwpfUGPkVVkUmFNBSqtgDM/6B/AVxTRBMAADS0iSagNy8dUqVFgBAsp+vXKDUN/l51mDgrkVbe7+dh/dofzJIb735ZgYAV1cqDA9i/T8EADYXuv9fL1qoqo/yRQFAH+eQp5//vvL0jH2wBgD9vkNNQPz3Ol/LFi8VQAr8KrOON1uUz1qXbACwVjSUDMAmf+6kzl//1X+3FTDWvnW5MlsE8PseUW4sFQF+c8ERCB7wAmw8yqqjWgKVKd26wzY13nmgrcnA7TUP5nJgs/JGzXtwYAtPQLwvM1TsAnGgYuzKqhMISxvLacHJtMN8a0+cfB3hgdLchQAe4/pLD8LdpH7XI+J5E2r37xUwUEiMyuDr+QzAqCRUXos1JbX+fks38gKX5bqNLDFWzNZzbTQNbaCjHsdmmHhiktlRTKrwrM2MuGbCNxIcJshjea389vyClZ6/+em7TOzHazFRhpF4wiaaAU2W8MhsejafZn83sOXefuMtXlavK2BQmzgqrrpaMIiQ/NhbJLOysneTNoJG4x+JXVfMNnhV8PjRHQ9APX1WEDBGwoy/w0MDG/zpyTUm1gAcscB88slH/Dw2WoIdXfhZDFO/I28yegai2cCVNj8DVUjM5WW4lSkzAT51bfN8ZEXZZtMB9tFnI0BOg75MthF8hS8eN24Y9oIhFB4t29k8M+wYoIU8FgkCTbvZ/RfmwJKW+j7aK2wMAHA85vkKAAw5blTeAHDi+SKxIOBE428FNwQOpmJweT3LHjVRkXZXjBJE5LP2g86AIqRJ5ga3/wAAIABJREFUAAABNA94P/mZAKzA9KDHCZiQ0UEO0tPSnP1FAUDcb3SDLpM5n5eBPt97/GwA0Ow/jY9m/cD5AlAWuyy8WbLksQ3EU+pG4KnpLF1KjxGgUSILFgUkpTMleLr2JgCFfTPHVSwsBj8ZROfgKVaQYJqUFcgsTS6krnVySwYIWba6VgJdfc17j2szMuHpIjn3gPMGzUzAlH3w8f10cX5OLyfOSzNBCADL25bzH6IZjF1K2ySVsCwcCRiSE3x/rtonAev4fnyfK86Y10gU8f1eW8wkYaV9DVsBBL76r32eAErq+8Ek6advfvObAkIHClwBOINZd3odhY9t+vC3j9P3v//99Ku//znPYTToEADbLObqqh2Jam5+lU3ItR8jIMX9wppw69bt9Oqrr7Paj5er8Aj0KWWhhxaYFWJFKlAOhl00A8rrZERcYEB77mH8KCjHPVdBB89QAOCUieewh2cKhoCYp2UzhzIYhbk/n/9azV8gb0L3RHcJh9Qwd3DM/mchtz3ABBQA2N6PMzht76XMBIhxH8eyGXVmy2VfRQN+7e6r/hZbPuC+lR2k9wWymhft8zvIAHTcV1SwNU/bAJgBrRJg5/ucFzvAxN6QGxQUe37tAbsHANT+akZIex2qFSeOT7JZfi5Wh6dVFSebCZQ/F4zVfQxP738c23GcRqrrgC8q/xE/1F0GDez6+/YxALW+mGmV39n6x4skVgY4fP4GlV2Iydf+BQDAbEVSJjoFqOh13FYcBKsw92GbMOgTAMTPKDDKv7IBj8hMnug+3L55jSb1144ASKzSeiHGPT7BcRXMO6zDWJfm80767Qe/TdMrqRgAAGIdNtPWjTaOx/DIW6R33/uNLFSYwPfSn377z7he0SIBwCGbBaHjOVjkYH7JzuDuHSTmN9JmJSuCo5GYU+uZAIunj8/JLL44F0Dhhl0CY3upF0wezl/6eq1pSdHpqQmUmxUhbtBGORHQ1Z1wHU2xrqNpGY53NOwJIDzucK9BhQQFnbSRdBqMSyhWEPfg/LsDNXnAOoJzAmPqgw8/SEcnR+nNt95MEJvwfrMZWNOcwc2H5mBmh/UAmfmxXthTV3ErnoGO34+ilH9/5fU/8jbPbqxS+BwZ3iiiL5rmb9rHxHi+vFIBB8wvMiDDugXScXk6CxwZ257FhavII7B/Un1CT7x1WqwU15DxZR/UyK8V1wYDK2JRn6/3KAKL2FvApSviFwGOAO+aaay1XtYdKOTx8ZaMoeLNOf7MRA0dxx7aVqWwSIa4AF6ZvV72xsb+z/W/YPITjLeyLr6/JsCgSQ6bm8GyY7lMH3/0m3R0dJS+9a1viKF/OU8///ufp/5kRIDv4ePHjAX+4Gvf4Lx4enHJPAvPG36YqyvFKvQajg7hKmwKcFW8jOZT8klm92IWkOXrR8VikWek1ZXm1yUk59M0vbpMyyXG+oAy8OOjE8YnzKmYq6hJGiXt+I5ekjJntUzvvvsu43oC8WExcnWlJjiblYBXPCU+x/ASndmjMOI/5zwZpMnPu1E4Kn+wmsAxhTywwewloYBxNToLDnhvrt08oYJj2NmKoXcVpBPzRQ5YnPl5Glhq9jV9cN4d8nt6g1564803Un/UZxy6mAkAHMX8rAHATGDa0xyNe9PzlIP7t7Wd3z4XADzUAyK+/3kA4LJC9l4EANyL52R1ZtsqjzEMm1W1Y8gSACQwmL3NHag048Z5TUvhCQCQB49btyPN+D0BgKy87DNr/oIAYF5AiwFVVlIOAYANUhqAZWwkpWStHFUlAMiJ2IvJiMS8NHrOD6Jtil8Givz3CwCAfHvF7DmELNcVp8PzZT8AWDdvOQQA1rRdMwTM5Npn3l6eiwHAUvKra2wDgCXzQH+PynJFdDoEAJopkYmo8ayeBwC6+UfvCF4rnfSzTz5Nf/M3f5N+9O6vWYk62qppxAgbNqUcooCDCcMEMSQvAACxMGIzevvtt9PtmzcVuC4eZT+a0lTXACBlqewCrMA2U+zDd61toi7GlgFAApcMCItlYoNzgExCQBakfXp1xVxZLWkW+/DhZwK7uikdHx2n9VYSle1S3fcMoG2WG0oGARjwKIMeA1VswNhA0a1WLzQ5kdFw02SgSxYhXgDrcD4MysEkZDVM/jr4Xmxi7AqGBH4p0JFeO2hwYp8WXM9wTIDEzUAgw6QfYKyH9gIEo0hXHZ4VAVCiYIfjOkjAO/Ddg1HjNYoAm5vffJ6mF5diCHQADPUp6YVkuUNpDx2CNc0rk37fv0F4x0FyLemLpJ4AADFWECAg2YDZNoE3S28juEQihFdduStlcyVz1OCXE0AAdU1wqKYrOqACJZtg2zsCAY8C22BQFYEDQdGorFvS2SRkbUnbditGH74fL1xz2YEUjFVcE+YMJCTwauO4Cpm0N7NleChmD8CiIOQAmoFqSAvNFNAzQXe5prmM70NZ+HCSizEAYNYBGu6TWAxqssM5hopzeDPh716nLi+n6cn9R5TAgDHqcYHPT8CshFQoGAROpLp9ed1h3DLRc2Wf4GchR+yDQSf2xdHRJI2GEzUrifVxPo8uisXeUe6LYCaXnpqcqbA8IFN2k/74j/9IjMdjJXrnl5ec12e3bzHRPDm6kz788MP0n777H9NPfvKTlLZLBurwmOE8jv14Q2kXEnd03FVgjvOYjMCAuUED/ONjdAAUG5Z/m8BgXoUDzCIB/GLy83cEZkMyFp5aBonkFQWwWE2U7EfKhkr0ARPTFmsh16SlEm4UjLzO4nl6fFIFUey9zDUACPBzAgAfPHgoORyaPYFhwOYiWMv0vOo9mWBGEQN9EQDQAecOUORVPXfbbhdGDwGAeS2tKstxuD1WGVUclz3+IpGtAEAEqOX8ehYAaOBaD/v3DwA28rymaRv3iD3m+b4f/xgAYPMscI/Bxt31gXkWAPii4F9ZmPTa04p144SY4JJBXlg2PIMByPN2s4AKWC3XYjdtwu9yMSYYgANILSG5WwtwZz9fN+Qb9AkAAui5fjohAPjSTXUN3gaT/vIKTHMIHBW32VP36mqdPvzoo9TpSmIqCZ+8AjkmYh++fiKA4Oe/fI/vm0OR0e2kv/gv/yvtFR0VsdBkAWsQAEAAdVfzJff1d955Nd29czdt14qLjsdhrbFZcU8Z0KIAheDwDKZPmUAQrDPDCMzBknrw8GG6unoqRtr2ihYUjgc3ThxTxHtbjZtlADddSH0h2YN1DAEUAQS4L4z71pI+AlCjCmQwJDN800UzDcRbKIKqidpvfvN+Ort9lu69fC/N5moMJwuCsFOg1Ues4SgAATgtlAwaTmLfMw4oCg/ybFuziQPPHzLnIl/Afsmfo1Hc0VCFOkoZY23XAxSDFM9B0ld7wM3DsgNd3Lf0XsTzRLNZxqPuUmygDk3eWJjVuG+afygeBptff1f800jUBRrV1g3PAgB1X4qCK3MDAT/w8ON9DuTUc6VcKxyPlOsvgBk2CQzfbC214XsHX2zGPI1E3x7QeHyav44/tQYZMHEhCwx6WgdtE5l88obrp29/+08Us2x66bvf+27qDgfp3iv30pPzp4xnXn3rrXR6ckrA49HDh2m9VEywnq2CWKDCcBnLIJ6yZRJKqXheVwuN49yTwBZNLCL20mz6iPEl9n/dM80tLGE4z7MbN2P+6foMAKprN2I8FRXxGQCAzCjAfOsboDRjQ+PPnt7erwHYYn6jKAmAVGBus54D8NaDbRSJ5XpvCbabFXY7AlzJAMSxBpLTn56d0HO+n6D2GqXl9Co3KeRzK2w3yjFTKzVrAHDRG3P+ohnP66+/kQaTKHzjOcOzORIvA4DOGw4BgHnv+JwAYKMQLM+6+fcXAQANrHO+HoyD9B37AMDdOdcmau3br/0Zz83MEC4O9rsAgOXxWgDgv/6rvyYD0APMCY+38By0HkBKsa7VCRPPua64djSB/T35xrprZzxwI5wNNV9MgYo/wsQID99tppv71EgOtCLtE9s0796uJbHr5P5PscEXTMDWBhQfzYlyNcJ2EGNXsA+cxyYqBbxlpVdVfI+BC9/jcvBw8O9hDByYCq2FJL/HjMj8i5qBGBt1eG35bV6iNsGQ2vnOkFg3Y6OS6vg+Vvc9S6MLs3S9tQb8ghm6G/tWp1KPnLgNvr/+r73Q8v1U4rnYLlgx2QwUOP34N5+mH3z/B+kHP/lFGownabIM6aYr+mD0hGcI3o9FEMAapB6U2g676atf/WqaTMQ+gemsn70DYvxMCXB0ym1+jg6iIUHFxoNA30wZboZhigwAF4EmQDVu8JbI9SXfQ7MFjnua04bRcjSUGEKKAQ8vtIuPx6aOtp2EbmxmCwEAuH5N3fWw0TLQGo0F0m3XZAABNCEVnUH6Ol1ePmXHTGQK7mKEIClXS4Np5WnF4COCQ1SksRZgo7yYTsm2ORmoCQCvhV5XeqAETdE0A52b0VE3AjhsuJ5DDM7oZyIwQlV0+Q2qK6DGjq8DTB98Fhsq3gOgbzEFeABGEYI9NFqQ9EhAIMCXJjDSxq7vzwoQBEfupAagqmeGmQLUHFAGCLQN4BJvVIfUNvhnYEUdeXH+bVN2gDsAiCDtAIA9HoZHYuEdUU4gg52oePK8o5shPQHh2WbpbTwDSGDEVBBrzs/C+4qbnuD+oblLLRFsgQPbLcGmKTzpLuT9Uja8UWK6C7BqHiiosbl3eU3twpMCxZbNQ/FmXAPGN8YxAiccz+A0xgoCNDXnCFAYQT2kJWzsMSTzFPcNAOaD+/fpVYfrxvmxm+8GYDOkpzJ357gN0IseSGEijvtE5mMAgATTwgeTqyOMmOkRqPMcT8ZsHpK72UWTjGZeKXEG88G+mJxDMT/wDBGYIgl78803073XXlXBYqmu2ZMTzO1hOhqe8Lw++OiT9MMf/jD98tfvcd71+urm1+8hgd6k/nBMCcqd29fT8ckJmcksMESAa6AQ6S+ehZV8NcELgbMSVMjUemxGBKCUnytYnEP4gyLAn11lxqR9gpxs8jF3xQAE47Qeexy7NgOPvZkyuGJc99fwVp2mp08u2AV9Ruk6TNPXXKfQLVkSZ20QZqTrmcGLp7BD4RuiEBnrGNZvnseeMKaVCNhTyMqJGMMHx7X3zdinCd7QagLrcsOiOhQA58Jj/YDsHVwwC0rJifacorFMzdKNe9spmsNh/GCdKaWBeYraY68CwZzQ7MZN4WUV99nxV2bi1M20AOQXRYUMrPl7q+KLVM9t2bOfk9d/DTsHDBHXwGqh8F4uC6BZ8lMUPstKf5kE7wLVbcZlJp7vacZTrpE53grQptWl0qAe/VD1qfIa8e/GeqCKx+PamyTXgJqYO+WxOFaCyQjpYp1n0OuueF48h6qrKjxZAVABwEHh4exETYFuXr+mZmhDfT9iNHqRhaTz8dNpevjgQbq8UgGlO1ShdwnuNsZvH10+N2myGqXzp0/TRw8+EwCY1lznvv6Nb6aT01P9Ds0K2K1d3d75ffAO7nTSt77+NplPOG9KfwfqWrtcoSAoJRGZTJ2RmnesBSjZsuR4HF5+GxVyOh01FaB/H4CyjbyRN6sBj//kcp4uLy9YqCOANtP1YZ1TIVOFSHgS4hlOBuGBiD1t0M/NOFZzAZSTM3VhRr4GoHVwNNZ1HI1YCLpxdp1/HwbDEl1i8b2YR/qepZo6xXyaez1wvhRNjlyYEUgDpp1sY2ABwuYVVNNEow/+rMJtL+JeW7UjUKO/IBt3ddMsYjowvrFvYt/DOADzCdcxGUazFjfZiIK+n6c9/MhQDUCsTvA9F1hMjeeMoY7rQIFXryioBiHE+WW5dnKedwU4QnlAuW5oEHOcGc28PJebc2lLfz2XxgZio7kmmw+GFybX3UE/VAdiGq7JDoTNSeTlMd/Q5ZZzN+LTDJSu9bxGg356+PBBmi3WjE3eePsr6dVXX0392ZaEiqurC3r3jscCpsEOfPttqaTAADx/KgYqCDMEqvsTFUW7Gq+9fniM9jvpww8+SOOJAEJL5YGUIo7L+89ajPflEjYlPSoXyAidnqtAyOZv3XR0fEPzJ/YfNL3S+ibvSiiI8PPDB4/pjQmgF/d2PJaFz4pNRCD0jbWtr67QVBmsN5SWl4VBsmUxJk1Eyuurd6h2PruehXIp9n8oLbi/xvMAcxnEhLNbL3Gd6fXVhHI+EzA/LPZZnUc0UTVhOAqXTZrdXs9XAQB2R8N0D43WTm9EsTOspXI803BzW93dg3jR3nuKnw5axMWsWevMMqDo4vCBeMl5gb+h2Q/NqGwrJQ6eV/wBBcwyb88WNLHgZGJTlsr6iH6wUdB+jkdxuS+WOFH+dxHPcYzHhXmeb7piKvNeRcEEn+0AAOQHKuTq8wKApcSz3KANAJbdqMrvye2Ls3ZLN2QfAKiFpf1EjaADACwXXC+c+Vy+BABQi4YCqcyUNFPmSwIA600jB0PFZZeBsx9kDQB6gdvdABoqaDmoTeFufvflAYDloD3EBOx22tKcQwBgfq4uqMQbn0fRrdtq+zozQTYnDgYYDVRGG/guPDD6adkbpc8+u5/+n19/lH72s5+mdz95zEMdreQhggATr94eAFCVKvnaoRL9ta99nUwvNhdZQXoQiw82BTRTKBYLJuHR4IITNio1ku0tUj+6Ahuo2axV8WrZSCCJZKIHaYI2ani1YQNCrMQANxgueRxWCxIDnjCPd0KEjfN4DMaRAioEz1dIfGHcHlJJgEwIHMTuA+ApCR4quLgfBD5oqh2SpQA2kFkw0EFQG0GhAl/dKwROoOqPPS8BlCAhiUAKSayqmsFYDAAQCX9uTIDnVXQRQ3KPwAPMo8vZlIwem1GbgeXxQ4ANLKmFAgkk7vRMDIkAvP0EXLYTes+DDABGwMD5TEM+JQCU1wCYhPyRWaXuHwIs3Et7CCEBKwEsrJMOhOUNJE9GS6ltSI5ABcDHoKuuYc383AXqFXhGBZuSJsivxERDoFwmyPAotIcMjupOejkpZCdrPWsxLxoGYpnIeu1ANRjjZbVo5ENObsSY1dm39wB59Og7ytWu+bfPOXtGlX5VlW8Zji3pbZ/duHGeTB4LU3M//3I7YCV4NCQQBAYCPvfkyTkZc9OLC8lWogAA7yWCejFxsy+mAzo+1x4ZtkycCiN6XIvnCBgGOFd0sqQkFz47GD9myMUt8LKH8crKOYBLeFgGs9SdvNH4BGPx7PbN9Mq9V9LxyUTzMq0JcKYVguSj1O0N2b3xwaPH9P4Dc4ZSE5qBq1s5z5lWIZLQ8DhYh9gtUL9XUxwxZ3jeRdMAJpr2YIoAvU9p/CZtA6hzIYES6/AAyqoG2gAE4BDBEEzDMb7gUWQWspoJyRMJcYzXIYMWZPjR62mVtvML/nsRzFcwqFUQiYq+AUsDPsXajgQfXRjLl+OwpgmVqcv7C5ke91kC/AUBwAyurcUk0XxtzPfrWZQB9p3OgbHeVdKiGgT08bBelXGjx78BwMwIKOS/5VzPAXzF/D34fS4s/gMBgOV9exEA0JK+8vO67kjkc5OP/d2J/bmyORifqxOWYEDvXx3L/cALRrMvtCwUCnyxLCQ8CwBsf2cDAO47l2xq7v0vFi6ygcGYz1rR+HSsK3l8bZcsAJyOtRaO+1Im3Ll9i92BB8e6nxiDlvXi53WY9b/7q/fTk8dP0qPzKeOtbV/HgXKEwN60wwZP53Ow7brp0fScwOAbb73NphzwMcVajjiHDDQzrOCt1h+kb7zzWjQrUsGg39M+u17PItaLAsAaABvWSgATiAHERl9enYf9A2I4ACRiMnmO2MKkE8y/Jbq0FusxEkUyvhYzFgQvL+7rurawlujSyoH3J7rpQvrL9W+25D6zicIXzhr7CABA3CdswFpT4MvYY7dUnPcQHCQDgGiu1VXX5dEx2OujtArGmccpLGe438a6lpULYe0C/kJZkFFDp2VmlvYDIHCzLhyX8XR4xa0B0mAPWOB6ZLOC+AXPi8/D69geAFDzyZ5sTUJfjuMmdleecDV7yv2RHtHrTTo6DYUMmLA4t42VJu04LDNvO7KhMeNvtdH9qRVb9Vyy0q3OMzMAGGt+AwDGvGQzFsTnIa0tCBcEwmO82orAxB2f/QCejiiUdlO6f/9Bms6kJHr1rbfTq6+8mjqXS3qqLxczxRP0BO4SRAdjF/EjCmwg8nEfXskjEnEFAeehCuiUVmOcDHvpwcMH6eTkiM9vOp+KCQdFApvRKM5g/oE9PoWqhEqBdbqaQdGDuFLx7WCEBoENsxOANV4AYHE+o8GA+/2DB4/Sb3/7WzUJwnn0RPAAwYI2TihlAKAGAMjnF8BR7LeKVZucxRY8GcDMD9QFb/23v1VzEXo0Yr7CExkAOaYAm6kpprxx8zYl1QAAcf/nswsB83sAQBzX+EyJQ+jfuwAg1gs0G7lLn+hbKlR35VU6DCCs6fvbHtf95xWiXgAALAtvhwqm+wDAek4c3hN3/9JYuZmQZABW63jGv0r2Mf9SrxeOm9qMphLzyTjBHsZ/Lsg6LihuAOPmrput+hri+zIA6AA1B0Z6YyMtawNDh26ST6Q52QB4ggGYTVpr+W9UUNykIzMA4VmAoCW+sHywDFCrBK+UwilxrrUFVYJrU05T+zPXsI2443s1wILV4rjcNzxOsPaQ831ogaOtm9euzNQDqmbI+HgZ8W40rPmo5Xc1g/uLAYB5uFQM0HyeBxigBoab79/PADwMAAarqxonjeS5YRdxOh1kAh5gABpIzMPDEzKeR1S2wDCi1Gt0lP7zf/5J+t5P36PE4ckSHcrm6Tr98NDsXkFXf6s28P1M3VQXNHqLdDvp9u0bZACmoJrbc8+Ll5p/NHJJBDN5EabnhqSFSNgRqAy6fR4Xkgy8bxWAoqWlDm6R2MtbIbptbeRh1+sMFZBFIM1gCAFUmCOb2YTbhIQYdylLOeDLcXwsRlw3OuOG5JBb62LB70DAiGAXgbjnv0EcL8g+JtecIqlk44TwAER1jJs8mFFrVcyHYCgUDMD8eUtvDfztWz8ocZBEhM9sMKDcGWyv6WxGRg82S3kYNiCmN2oxNKOiAsAvKisICNCtTudiSqKZgJ4HsZE6oMhBuwBn+7yBOcWAIYAgSEMB9mL+Zb+zAE2dgJXrHu4l79MQniZo1ILnIrCHnkGZ4dKcVynhatilAmIBODYAYAG4GnhYi9lnlqg3MLNZ3f3OQK2bxXidqdcuS6Xd/ZMsiOgczEDsADPdhZEW2yu+pAQsSwCw3tM8Jsk0p+8f2ABiZMDbRoGSgH3Ps04/vOHifnjsDocjfX6z5bj6+KNP6Q04iC6Qljbh+mm6TZBewaauE/uPugFjrDZeSZEwFIA9Ak02ChkO2TmPLOABKsGSauHle2AfHVbLwTyMLskMeuHtEn6XOB4SZrBncEwEpmR6rsXAQ/dwgIUAXOERtVgqEJrOtJ7h/gg0F0M2pZVAyzDJzs3EooABTonWgQBcw/cxS0YNAFaeD5bm8ZM4/5Bg+RxKyQj+PRyPOD+0Tlk43TSGATMEc8Vej3jW8gycSbpz+ZgMAnySgfcCnZOXLFDIrD8CrdjHsE8ZSCRLzXFD86S1bhQVeP6iimMyk7P2APycAKC/DibfLJREl/nai/cgAzDvy/W+Hs+tmJee02WAni+36hKcj0ZpaOxZnADaX8tgXetiM5bL+ZsZNLuSDM0nF93sIVbd39yl2mxFx3nxPjMBy8K0x+ve64zv7Lpi4ZP19efz3M/0yAl8ndnYEmKPVInxcVkJJGDRPK9nS4Gr57qjNIn9q7i/TczfxFzeyw/Fv5aols/O8mI+p7jebOETbyQo0QIAm/hWCbYm0HgoAOPkaMxmX1yvAUgMeuwy+pU/eEOMnVEvTcaThHhULEDZX2Buc/4nMZPm6w7jo4upun2/96uPua7BigLd3y/DZ3R0csJ5NR4fEXjo96PQFlJZnBsKK1/76h3apAQxMA36EfuhONrvp8vzCyb22bojACI2X1tv0miAc0ZrDkiM0RhDTaXsk+p5sNkqPtxQMu41qUtmnp5b/C7JZ9CM7AEAJ6xrszlZkk8+e0RABpYHLIqdHKtz/JX2D+wzOK/esK+4gzLJxpu9G/kKgD2s1ccDWCf10roHhcgqzWO9d9Ox7WohpuFI0kIwybgXD7AP99PlElJMPWMy1GLf6NgyJwAOFbABwEi9sQBUQ0+2kEJHt2FsYFjHEVczvjJDKdYzNwHRkgzwtOjwHAA9x7qtrFjgRZVWBWIwzBh3RyM8H8dNuNxlvfFirgs/zdziOK8AmVxAKdi0VGPkRkptlq092uRBHMoX3kf9LAsdCuujcVlTpMRxj6JJXacnz0PHDzAdYuwXTLtRXwzUJVwDB/Cku0NwfIwYYTrVs1jDP1oxDyS08KK8fu0sbGiwv+raMb4wFmm3sVKcjXiEgNxoQNb/8bVj7s8oSmL+Lpay5kDBUf7HBgLFnKXlDZRRIRmWzdE2jTGPC+Z1H00agwGI7xv2YeO0TJ999oAAYCZEoTER8oteAICx7mK/ECCoa3Ec7DjZcxPjn+MoV7D35+/bGN9WMTm+WwTTdh3jEDYr8AAEX4RM3gWKtN3UA4EF64fjjVjPs5Tb+3y2N2sUCTy/3ohNdjr9Ubr78l01GgllEgqraGakeMYWJIcBwDIuKvcD/ruyhsvxS8EA5HzcXydtxQ2673pjbZ1Sfq9j+p1zKX7RFCCDION9surabAbzbpNUA/5Nl17erxofK0gOZQzkU6kBQM93/x29KvJ76UlaAYD5SwsAkAt39nD5cgBABvT7WHn2zIvk31r5+sHXHm+5C1t1zBK5bj3AOpDJEtioeDwDANRi3ZbkONHMgXwGompm4oGR6YpunOSzAMDyOp4FANaDWD/vDyxRoWi/YpHNxIOgjX4OALA9aAOgywvYfgAQ0mt9rj2+DgGn/9AAIJjUbLZwdMrmH9/5v36YPv7kk7QanHIDgl39swBAMN44t7rZwK5nAAAgAElEQVRbBpx37t5kZQteYJTZBdhXL34CuBZqlFAG8cHks28EQj8EQKCiI9GHdAQbHTywvICRCRalTJjRAggA0xCJeq8LCa08BimBDWkFAmACHf3ofoZ+ttjQ0KU12Hw4PqviORGD116XFHMAFdikuXaD2UN/O0lW/QK4542GyV1hzrt3kWOVXhVPg4vDwhOlBf57Hhr49PwKXxaCIXwusVGHkbATfDMYcc4GAAU+SurBpLkwq10tJP1DJRLgFyrqrEyiKB+S31Ky3zQBKRk32AQkJS4BQMqSIWkZDBK94XBNwfRzYuAGJ/Zza6SeqHAv6a+GRAPDzQw9gCKYnrr3DgybhNoJN5lpPbHM+j09fwBnuB99et2peY0S9uiaTQ+4RkrYJB16EJmh1/Jsba8RPG50JW4AgPDDDNbbYmWPl/YqdggANNir/U4A07MT4ZQlRmSQjI6iA68Sm1LSybHujR/PB4WD9YaBLDznEIhinuI49+8/TO+//366fHhf6x+CbHc6hpdldPnF2NPq3ZY64DO8/5SkyoxaQaSl/fr7eALT914aQo4/GAYQKHls7tCLIB0Nh7AGxG3EueC73cjHdxcykldeeYUBNtaKLsymAcoiycD5jMYhYetJgrOB/QCCboDlqBZ5TER387g+eLQywF9jPcGxHATJC8rMvhIIZWK8UhdhA39mT2N8EZjLZtkCpigXNssS5xXzmFX/eA72GKWFQqcrqXokljg+PSlnM53v4pLPAPGKfEnlT4p1HcdEgcfrMMfeFwQAZWHQLJ6/KwDo9RUAIOd5JL7unux4cF9hbd8+38y+XQDQx/J7SoCsZADy/vhNtQdQ0dm8AZrQfGB/5e8QAFgz6wz5mlmav75mNh4AADFf2kXnKpyqfiwBQAIyxTh3nKCPtBP9mvmXD/scAFD7byNrKpsEPOdM238+4Mnk23RoDS2LefuSmhIALM8Tc95AiuKLtgRYFgjozmoKYhsAtOcnwh6st8cTrbuQ3OEFzy9K/bprSuNu37pOhgwUGvBAhoSQ8RS8x9ixc8zEfRbNrbq9sY4z1/GmwZS7AusGwMN8nh7cf5Bms3lYKQRTi13exS7Dvbl9pmJNf4imE4N0BMkyCoDdDpuTwXLFe5X2LqyvjPr4+6uLsF7pKo7o9lRoVtECRZzY35MAwG0UrvLevpFSAUsirFIgQSbAgaYqLGgrgcc3sit9NJWi//Vkks4XC3lbo+vpcpE+/ewBJZv9EbzI+vRC1rou4CSEBNGtvatux4jnutGUA4VUrp0B6CD+7PaiOzscm8Oyw4nASM/BhcrJ8TEZ6WM0MGGXbo0br2P0ooW6JLocL9aSiOM+Iy4CmMPCUay1eX+PfV0WJuVY3J2n/OLsAd+shzgPSF2xfzCWK3xl4Umt+dEQEbQeHAYAtUo0MVO20Sn83PycFT9BEbMLAApIDNscSIBx/8PjD88ZwNzCXaINTMW6gjxDnuOWAGOv7pCYg+MOyHxFN+5gfmIuHh2nm3dfoQQYBlwYzyge6qUCJ/ITPA804ULTENwfjCMXVO21y+YyIERs5LnbH8iyZXQMD22RBCARhwcnpbdQO0DGTIn8Nq3nkvxi3yOQX8STtDKBbJvFG923GgAcDwR0f/LpZ+k37/+G5684Sv+V8mqbwDTFC9Y7/G8oAxA/u6tya33M4y1ui/eDuuBdWHhxrYXsHflBT+c9XwjQRtH2zp07BAApTV7Bi7qT7M3nJjM7Fm227Mj7VLsZFr4HzwldnFEgxlrKJpHRHGVs5VDM19KzXvcz8IY9Fh6+8nI+7dS/AgB0gfR3AQANwnL6Fuezb9/KT8XM5GiukxWZ4aHeWLoYD9I4yMev8KesAKkAwPJ8Pi8AyO+J+HcfAMgc4l/9S0mAudi0vlQnvGtG2GZUHbo5+SR3GBoVELXjIRMTJTPzfHYHAKQqQq3Pp6npt4ZT80OMGE2AxqOweIMWAD/oKhCvAUA/XGwU5cvntTOAs6Q1Ixat78satzhY8/m4H4X0Zd8VuvnF7wsAdEUP392i4MaC2bBt9j+/bVBTm+dWb6gxNv18Kg81I/hfmAEYyHj2YvTGHdTl2VqeV2lyQpPX/+E7/zcDnNVA3ifXe0ocQXXmRheMHgNw8NjCgjueaIG8efN6Ojk5TYvpU14YunUyEQjPPwQj2BCwqTx9et5UjqtFie8ng0sb7whduhCYbATIAYgia60viQOkpNyItmK6YV4gid2utTFjk6enShKl3uwLeKCwWxw877BR8jvD1L7TobEvXpA6gnEEQA2SjiGMZmMDxnnyGAwyGlaTgDVTpYPhEZIUj2UEctjA8JKcSd56NsxlV7gycdwzCQgsRiCFe0Bp5GQcABB8zXROCswVNCBRQmBbgp14n5MU3su1/B/x/asVOt/K7BiB8Go+I9ggTy1JtzhHPL4iobW7qYEbAazwehOA4U2p1x+n4+NjNlHA9+D8AM6YCdiALAK0EMwYVMXxIEeCaTcYSWbQ0asxJIiNlUPDuiml3jA9FvMsmlJE3oUgX8lfwVLgd/h5qoiUr48dGyFfV+Li598kit4gY/+hP+WaEg6/SrC8MXluHjz3nmpdbMlk6RkjIAgSUK/ZlqCWQwjPAPMCTXnYdXI40n1fS7Lh/BMgFNaJbXj0WGKEY6E5B43SOYcB0qEhzpyJwPu/+oU6WzvRC09FBmcxZ31+nD+xD3HzZidIdcVEwkivuVgItT4I+JeEZ8jz6wcAORofB5OxaW5DwDUCH3YRXq/IGtVLnlIY76enCiZRwYcXE47P+QC2A+8DrKYhl4anTlgpgEzBoHupDt7onrtep1FHgSpuKBORYFy4+2LjTWxGeMx3M7GicZDukYoYvHcdJdLDnrrhsbhByU+YtdMTaMnnD4DcvpXsEh+MYN7jSC5xbmQF0PRb7GuuGbHuw1IBTIbLq6mAzxj/LpBw7hcNMRAf+L7xb83o5r9qBuDvCgDymAUrxF8HCbLuO569JHuO3bz2lAl0eRwd4/kMQH+Xv7+MEzyO83uc8O4BnMrAV+fbWEI0czaYaXUF3QlV/n270OnPZ+XJHmUAr71s9MZBZyacP7CrOPDaZgVJGaM28VxU5AsmYPt5NdJf3YcAIdzt+QAD0CxxPzdJ2w4Vo0sf6rZ34MGujIe6KDYPROO5aAbCUVMojgggFFYs5fzLz2WPB6B9icmkrbrhuRkI9g0kv/YWw3qiBLUrJs4GTc+O0mioOOzaUY9MltdePpM0NwB8FHCwviwXIaFdg/kDgEJNylbc4zZpyUIMpHjwH52l05MbYiqtu2oWMNfaf355xaJLB1YwWDe3Unqg+yzXlS4UB316m/F+be0NPJFCAE0GEO/1BYCl8P4DoInjSCbZ7G3rrZt72X5Da+VkEIXiGHcoIONz2EcItsR6MIo4p7/Vfv/g089U6D0+4fVcXEzT2dkNWk1AMo1YCrHBjbMbas7CNX+d5tNVml5epqsLMRYhqcR9OZ+pqLICIBiAHyWx4eE3jO68PTQpIZii+XJJoFNN2ACgmj3PLu7o6h77rtepZdhswKuahfOT6+oGv02SaocVRwpLCe/LZf5UAoDugm6grQHCgwDhApSbzMxmvF/a2w1mYvwq1muspixNj9+zkNwwN1243AUAQwob8YHnVQaCc/qt/dTtARsmk37vvOrhk8e5KRiXOxbq1rmp3iAIkLmpRcF05/vXUlABsKZF0Br+ff1049a99Nbbb6XX33yJ0u8OmldASdUd5QIr4tTZdMqfHReAASp1guJHNAHDC3sy4m5ssRj7XXj6EbyPhibRPZxxAGL8mNd9sg7FJKQNCpp6RRM0xseIWyL/MCDOGCmKtYOhvCLBAPzgtx+kzUbWTg2BSXnXJoDTzFgPT2c3u2vnzk1RawdHqBNdWALQgBzxIgqhEWeyERBiHgGRw9EkvXzv5dSlBHmTlps597JxpVzMe5UZwZkB2LaacD4FSwHGwaMJ40F4AMpKRQWBSS+UT8Hs9T7kdf13BgCr+p/H8SEgsL59ufBb7Vd7QbY9+2bJAMQhrAxwE8XS07l1zBzfWAHa3v/3xWo+xRY4WV3/zvCIfTWfZ45hFCdnAHA3KNhPOa091Q4FE88CAPW3OH4AgM1xvjgAuO9cXgQA1A1uMxSduGYAoUjAtDFHAFgBef9QAGD5wJ28lWP5dwUA86A7wABEhW3/KxL5nIi/OADIDcSNUfJAN5Cib/2yAMBtMCFzhSwAwHV3Q2+QWWeQfv7zn6f/8X/5D/SYAAAIgGgclRR7HQAA5LgAZ4eNDmYMMK5fPyXzbxSmzev5lAnggh6CUTUCcw/+Lv1eQudQVN+GwwjwKgDQ4E5u8hCVybSeS/5hSfIgPCcsXQfAhO+DZA2g31IVN78QkHLDZTVslSAl5rxab1Q5A1hRBC6cMQAGEEyNx2QYIVEGswYJtoEoHx8VNgXsAM3EMMrfXVZAw9T56nIqaWK8Vss5Ked4UQqCwBQAh012C59EJ7AMNENjg82KTK6jCQNTSPYEeCoRxjMTABc+a2S/ieVm6WwOsMMwXONUFXcCpbjuBTx8ugwsdFzdYzwZe5MRVKA3JOuv8Xe9D7iuADIxnI5ObhDEA8BKgBHV0MGAlVn5lanCazCXVdyYO7jHSGbY/cuM05AyrpZ+9k4MqwpVMFzRtIHnRcCpYZzh3wYA+fdMCWmajzDZ2ggowrOHhGg2u2xJ/GoAUAbmehY0RQ4msYNgy0F8Pf59nheFLLyU/MovRn5E+DcArLzGhQddvZ6Z2YjfI6GQzEb3qRcJOMAmNosZK1FEN0l6IYXUiywPzIuVTOENJj99cJ+S4MunF0pkCuknElxLUXIgn80wdJZsysL7CrmfzLC9DzLYzd0VBXhSQo4u05Aks2kIEskm2afMG6BaeAY2nY8F4CFIRqBJ1szt2+n42mmuMhLgCo8xBJ44588enAt4hBSGoHkwPND9Aq+FAFEDgN5/nVg5gDIDT2NMLAO8sOb4d/p9XEuMf3T7JENxCcPvJbsbIzEkCInk0d0d6aHTy01tzFgk64XMCTFgLZG1NykSWIzPy8sryo3QnIhdLWENEF6BmZXIeSMGMa6vjFe8OzaeMbo9+T2/owTYx6oD2w69gsJYvgIAy3lQSoHbcVZ7X89byR4JsI/n/cbHKY+XGS/Z466QARf2E2Q3hcdreZ41wyB/5yEpcIWF1QBg7uZdAX35nD8HAOh18fMAgLuAtxumtL3/vP4YGEaKzfWg6lZdA4CYn8+N2XWk9vg71JVxDwBYsv5KZmAJAnoMmJFZA4C+Fu/LYABa3pjXgJCuESgKDzB7oeb8YiP7BluZeH6RZQcQxt0htjMxiDpzglZfeeNOunfvlXT7+rVYK2LNWGKdQGMEdYdFF05c1ypM6uYdeStDaor9AIAfrmE0Otbvg3HruO/p7FwFgwW8oMEYwvqo/+KegGlPCxnbwhCAw3zVuEBhEIBKL6TD3f6WzEGAmtj3sQdx/8oMraaJET6/nD2NtS3Wo9xERV5sbA6CBiChfBqxq+smffD+b3j8k7t3ef6PHj0lAIgCM6wtEG+A4YW/cZ9kExQUqPtcd9cLxT0jL9cxDwEA4noXsynjvPOHj1TUWahLO+8Pi+VicW3R4I0xH+4VQKZoogXvZ+zTbKIlZh3etzA7DvLlXo+MTo41NOXq99Ots7N0NDlKq5nUK2gmpVdDXCkBQBSqFHv5/1FYKJh2+hvsKHp5H2KDt2hU5/MrAUAzzrD+kNn3HACwkf42AGC5LiL+VMFE+62Xw8BnsjWAY3Pnu/cfPghptQqNjH0L5nwfUvQsHfYeCaK/rW+0d67ZDGSbLtkUY5W2vUm69/LL6b/+b/6CYxWlND4HKAbISJVVERi+apohJi7uN9l7WwCAuCnaH6RessIANAbs7wCL60K0pPMLeHWjCcdGMY3H+Xo1Y3yAOAHxwpwdemULgO/tuWklfkZzk5GUDjUAuM3m1BUAWCg5eD9j7LiYpbEcOQDWtYpIlJtKeFSuNa+yFY7XacdB7F4uZQbWs8EIyo9eWm3l7TmK51Rvj/bczLFkNIH02mtAyfOnNzpKt2/dTteu3SDBBBJ95Etg+DIuKwDAMhYBFYWz64syAH9HAHAf0Lfvd55L9d5ppqwLZPsAQCm/ytnYFA7zPl54E5dx0aFzyb/P65uO/yIAIJ/Dv/qX/z1PrbmwiqF3kMEXF5Q3jHZlsT7xhma4WyHV99vN3QloTILwPKtuXw5OampjXamsJcP1cRoGW3NeJe06UyiD+efjeQK4gpKPmwEvHa/W0tfINCYAJ/+OFDgqSEzA1WWrnDi12Sr+xiSlHmee4F9QAuzDWdrmn30dhwDAsjlMDM19j7DRoh8I0FnNoVdSG/jjho6NIRh8XxYDMFdu4n6tOgKMHl2t0ve//730v/2HH9OXJY1vpf5wkE43FwQShrGAGQDcJnnC4dyRpN566UZ65d69tKW0bpm26wU3NCTKfHb2JEITkKhKYQFFoNr+e7Oh+b6ymYbbxwcDcdATqwOeGNy0w5MD1DJ51oRkcKOKEZJfBFD4P6VrS1SC1+ni8ikrwePRiEAomhWUlUhVtPB+JfRDdOXdbtnljh6F9gixtH+DjrlmA8rPzfPf0jxtgqp8QkYsCUmY79qcPrzosIF67eI9BGjELmGNLBAbtBhZ8jYBkw5BByuBMLwONpg3IQIiYE4FSFken2BAdMxsAj/7lcEyRk1OcB5kGMB7hBXDGP5mABY+j/pLAICUXggQpP8QOuEeTdLJMZqTTBjIk0EH8AKmxZaYRyfbXKH1eGSyMiALAgE7gy+zvFZo2hLAXnRbbDYkM66i623XPncA5RoJcJ1wW5pUTnazLewhCADwEk0wQobt9+67z7wzSIiylDO6ngWwhQqtwc9ynmQGoCW5ZafoYHjh/XXl2s8hS4bAmEWiGM8LgLxBT/zeLwBvuL89sAAZEAv8JmuSrING6sG/BxCIABdA/+NHj9mdcRleRPYsGgQDpJGyav7bQwZMN0vNcS6U7LNBicYpitdMomP+ITDl8wuAG8uPGBfeX9SAhoAlihGDPhOi8VBeSUpk+rRFwPN8+bVXxbgLqwCMH7Bo1V27mz67/5jvh9co7ze8eigZiv+iQs2AXuMyewQ78G0BSZCDBfs4gAx8L737ctdISZgg5UKiifWjBTCFf6LjETTVUQIR318xAMkkwX2MObPj77YWk/PJ+VM+R0iAS+mdvTZx7WJqNmu9GIBaGA4xAPMAOwgAase3FLaOTw6BPZ4r9mDUNYoB2JpHcQL1PfR51U3IagCw/v5ngX/cByoGYEtCagP/SPIJjBRMvX3XWv+uUbS04zPHM7nrei70SsKWmbexTloi+HkYgNrftJ/UTepy85bsJdY0vdLnmi6DWrfbgKuvHwAgE61SJp5jiib7eJYEuB2zPxsA9Ht37m/RIZjj6IBCZcdb/IDkCpJpxcdi0NagMZjPnD8B7BgAdDMhqwcQq7FAWazZ+Nx0KSbR0VDM5e1SzT4mA5DbjtNf/LM/oYXD6bGY0FfwN10ghlHThlHIee31CSCQYAH8wVDchOdyF8y+IwGAWz2nxUZer1gbybhLKzH+0Pyo12UXdLGWl/Q0u7oQcwyAY7kWPVopbut0ZamwYfdeFRbxAvNbfomK89wMDIUdrPvjiZhZjlXFGEdcqiIVxjniwHFYyIx6srJ471e/5h5x/aWXWaD95OPPyApHMwh4ofWGA8a7kAJn+weugYpHAfiwELcW0AmTE54fVBNs1CYgbxiAdoderCjeLGP/0Dq13IppeX7xhDEFFDc4Xj8yIUtRwYAsvwdMQAJh/TH3D1wente9l24zPlxcXmif37ipXgAVbEoVLPTwcK3BP5zXjqUP1CE9sUYpKyeQBsDZxbfIA7IlU8w/2sg0hSPuv2g2h//RckYxCABQHDOvSzH3fB7uCpoBeedTsc6V6zLnUKxz07mk8ssk5izieREItCeOt30+fxRiWahfBdM+muDRWgLddecqtF4u+7z+5aaXXr53L/3lX/4FpfcofLApGmYCfL/D21weywIs81qIcc37B6mt7kNmrmclgfIFN71zwZht5xh/hfqIDTMWzJ84f8gMhJWRrmO+kNfxYn4lL08wUEmIEAAID8TZ1VX69NP7agKyhWcoviUAzbAYijSPzYO8n+A4BDgjb2GcW+y3fA4VnpD37VjS0fXXOQnJFkWci3Gy3MpyAF7iIKCMj0DQ6KNbJef3ACBpIZFv9nX9Kyu3qnHi/XAeFgPdwSTdAgPw2g0WIJBPZgAQ+3a2+tJx/Sy/LACwxlV2GYA1fqDzKAHVZwF/u/u8Pg/glZ+L+VQDgBifvI9NwBm3uNrHq+Zk5T3aF9eUACDee8ir2VJzP9ccH3qcAQBsf8GLAYAAOvT5dsDweQFAJfsyTWVQkL0TGgDQgz7PjgKwZIPtUpZRVSr/IQHA0kS8TiR9j/cBgLiuHHhmyuZhALB8fzmM/r8CAHpC5GerKVA+vvzvDCwW3R5jasUEDSBiDwDI+1BPsJ1vOQA4m7FZSYBLAJCb5aDPBe3T86v0b//t/5q+9+P3GXD0j+8wEDhZP90BAPGsN1uwQAQeIoB86c4ZDfSvYEoLRlpUirFRmIaMiutivpIUAv5T6PZaVUYs7WsuU/cVtWzOkwioIJWgxOMIGzaOE5X/njwr5jh+H0w/eXShioRzxQsb2hTdsMj4QQOJYbp+cppOTk8SAkBsmJK5rSkpZVcpShph5qt5iwoiAgYnOGQfUQInP0D71Dh4QsBhZpw2MjX/AYuQbKnNioEPquo6T/mWLWcKeDkWIlCgJyObNmgTBsMKgBmZcp3E6jSalyAwwWbIBgC4H2QKCKgAc0oApLqIEeRAwE7/jKbpA8EYmF8Ho2wNL0AAP/DCg6QgvIGwvvNaWYkFoKkucHjmOv/wMqsqzbiPkO4eHZ0KkAwGAQBW3HN42QjwbIywzf7D+9Echt2aIXMCA3Bpzx9973ThhF/Pzd3JfE/N2kPXViUJYB0AKA4maQBMSrxUkS1fDLIxjkM6Dmx1Or0kaAIGWplINqyQxmNRSVMj6dV4aeY0pKpN9VS/VwLUSPI8h/xdrYp9gAp4vrqGhhHAVasrudhqDiZFJx1NTqIbsO6HX5YCG4iBBIVMEV5jkzTgHDAueh39TQDThozfR48epvMnTyVBj8qwSHWN5N7fl4EDrhOFVIyAHvbSkHZhXuFeGHSvvM0QkHLshMfnZr3lOEeybKkcxs3p5FjS/iG6UPYiOV2l7nBANqC77j19esH5BMsB+PqcnJ4FU0vngcowx0M0QdqulGBCisf9AgxTmtqDOSBzbrycdHkdYSdkzIc1TOslq4Nh+fxS5uCMSDqddOv2Lfkd2oTbbLcIxFe5gNAAQpzT4V/JgkLukB4gve8l5sNmkS4vLtODRw9ph7BEl+iIYxjoBwuQazSKEkic3IkY51R5/dQMwDwnaqZanH/2ujTYE+8rpeI7W2LJrI8mLDzXKDx4TS4/90UAwH2BayvR3COpKQFAPvMC+HSBCM+G7PIKANQ4ad+onfPO46k9z5suygKUcw8vepw+GwAsz7FWyPgePgsA9Pqv9+4CgFzT3DWyiqOw3tAjcw8AyKPVIOCXxAAs1+32OGmeWclGPAQENgzf5ijlM3seAAgArvX9cX1mzvC+A0TJXqkNiMhxFEqJAaTBiBk6YJatU4ru7N3VIr322qvp63/41XQPErroDsvO9AB1GM+I+UxWMT1yxSxXYzUAfFB2KLZC8ygW4gK48blL8tuj5FYJOWKvFfdZACZZaRRNu+gLDEbb8JSJ9tXsMYsdi+U0F0zx7NFMwfkZ5gzlegAEKdPrpcVSFjW+592BrE8ADCI2AUAFoANAHPapIbzYVqv0q3d/wXjixt17vIQPP/yETQYQzwIIgQXMW2+/zfhQnn4hOfN8CkSkHwVr/Mg9OApTXjt7sdcDyBMAaAWK9rlFhALZYiS8/WbnF2xUspzO5GsdzY7cM3EewNJmq33JzUVu37jOcTC/gPoG7ClXbuVFi4S/BADrwuS+tZbxSTTkMZuM42i1bAAeNzkI4ChbxtCHu9lzxYRHQV/31FYABADJGNMZeH4qVgWAVuVf8bMB9N3z1vsRZ15eXhIox/04Pj0V6z6Y/iPkL8gTQiZrC40OpOME1NZ8P7wnMa8++PSCDNGL+YaAFABAxA9QBuB5QmGB64IUG9eAeBP/NROQsVO3R2Ym5kXqWeLr5jlQ4iAmV7zmvdYApdd5jCXMIc2zJbtcMw5ZKp/Ac2bTkE43za5maT6bqst1V/OHRI71Kl07PqKi5ZOPP+W4xzhjvoKMjM0VFePCXIlxT+xnUh40TT4qB4Motm7YjVrPs2k2o21CAx8AoPa9wAugdgi/Ze7jPcRsAyqz0HUZ3ohUeHXQrGyRrC+rASSPoxoAxHdxXY9tVs1GNvQcxPy/fu0mLW+w9iEOG3e1n5UAYLm+l12ADxZZeQPi+qo4KBfq98RH7TH9bADQ86W9l5l9XzL22r6cXwQAbONckbdn6xUrrQpV3p44KZ9vweDcF2+VeJOfG8e3vYH/2wIAZAIUC24ecEV3QT78wpy4vFnlFx3c7IsP5ABrh7Pohar+776ltV0V4LwIb5T6Zpjq33ydE4vBTpUS96GGjXC+9SDhBmtvjTi9uitlfR71QgygZt99NCB4qPtfDajtMuCiO2q9cFjbb7P4tNsEpAxod5gFOQANCVzu7hMbTwwE/NWyqXI8mMnna4bEtvVywF8xT5smK9X7q/flZ2TAMHyXynMon2N/K8bOvC+qOjYUbjj0+Fqn7hhdYYfpF48u0r/5n/9N+ukvP+DCPeld48J31JXMz5UcP8/V7FLMmsGWC+/tW2fauJeqUHojhzcMAQZ2iuul6UzgGgK3Unw1duUAACAASURBVPpqRk9DGY9xEyaieV7WXctYPZSkjzJWNmcAaqPKtCUBliNYmghGEzYKADU4PwBI2KgN9tkEH9dkJh0BL2568hxj91F24ZQfGV6sSIf/jj3FSMEPrxAneWbeLCilVbMDNcGQWTKTdLDgFujupWodjx8SZ48pgLcABgA6IWDoDQV0ZoZTfyJAtq9KJr4Pf8MzZ6dPgIwINLKUc8J/oyoo4G+YGU3aiCX9gwQYzxRAFD0Gl5IGYVl3YMQKcGZWqRACAEQJgK5gOJmEh5vGyDLuo9ma28GY51EnnjBdxv3CWKIk6FiAjpNnfAeezWKtJic6hl6U1sQC7SYmrkT6/CirGgwY4PO8+f9dkN8Suizdje7QYGhZSuvrxXoqYLHHQAz3356PeK74TgCzpSdL7gxXdAgtJS6+JgfhBv/wewJOwQjJCXSxkJbXg8oePR0JqIklgUDTXp/oZoznCzzFCScZHpQ/yzvPXogez0wkQnoKaT3G84MHj9NHH32YNoslm3YsAWZh/Ic0V4mRGKJi+ulVSkXy86DkNyREZG06mNymBYDTMPsu119W2vFdHQGfBpdxLuPJWPMmfJXY3Rv2oIUFADwqPW7RdAZdMMnwCxBT3XLBYglJ0myWWUsC3ZoCpJmKmlcBUGNOBfsG4/Tx44eU3sqPC8cO6VywFyFDo+QsAmZfawYwwNQN8MvvsVQT74VE0fecXdAzkysKHfA5vLikLywAQPkEygaAz8WBeTCHfS3+ffYA9fkd9FTbX8hq4g1bPeTKlr4//Bl93d5HM8iX5Wvt4+8LiMtxkuOaquud16Es9/R+fsA7rwSAfMxy/XMCwvlasMkM8F/Nl8GyVSFNsmAH0fJS5WtHkmzGdVub0xRiY7zFx5uuwAGolzfDX/EMbz0DgKUPZB6LhQwsz2d7aUX84oDd65YVEOUa0AbO4vwjbi0Bu9b7amkSIvwCNMz7cZVg5eMlFQM81srnlYFzPre2lDuPx7xnPDuucwIoZj8AK8UVYABrThXyw9LSJDM51X3U6gOfv4sz+XFWgDxLjZtNOjs9Su+880565+2X2SRku0Q8sUzr3qK1ttjTucNuu2DzKV6wR66ZeY1nYbu4A09XzZ24H6Gwwv7kZgHNuUMBIylpVkK46cBGjDl4k2IfvWJzC3Uf5dzfCKicAwIFQDFXIWUUaz6/j5u/FRrBgNsI+HA8d+3WS5kBg5gEzVA+++wzdgF+44036VlsP0Ked8UI9f5JAgiBBa1jGPYoSsMqBK9BMOmdb0G5xVgGBaUAfXkdRXEW44SWD5WMUuNFjNyFi8cLsMWnWfqL7se43yjY8Dm6SWEUDNnghPGzE3gVew6+Splw+Oh5LvO+ZIAjgI6w0Dg+PeEhW/l1EeuUa6a+OwqmZNU355TjnmhWwDUWMWBxb2hZY6VA2F7ge7GvOf89uS4lynSK7tTwuA4GehAQnC86XqRkF+NqfMT7+PNff8I4Dt2XUVD89n/xp2SOwgMT8aol1avFQt6M2WLKsWUUecNCBExPSYwl1bUlguNcrPtq+BFexChks/gOixzNG8W/UO6IFah8AkCgrEn480JMSAB/fuH3x6MhYw8UPn/6059mADCB+QciQDBv0QQE+1Luwl4VYpjPII4qPKDxPVBccbzy97sxAAQ8jNfynqfnga7kXJ86UnN0hqN06+atdPPGNVmdxDiBhYnjxXrs7ovnG4VBnFd3rPvSR454LJZh2LIgr0PhAMenhyfO05JfK/fcxKPVDLDZbLzv5vnxHKnw8zwArUz0aFo7Xw5g8+D8rZvHZk/5/XPepISSkbt7fzF22wDvDtAbH2oXGZtYyEzn5tjeN9ps0qbZWQOkEp/5/zsACIr+zoPZAwC6C2Oe/JbY/SMDgExiKwpvuaF4A68HSAaMquuvm758HgBQG2okRBUAuHNvq4n0ZQKAraQlewi1I9c6sTEAOItuRdtYmNAenRv/+JgBxs/vPyUA+IvfaAMbpmN5W60uFKCEV0UPUklsKnMAgGBmpfTqa6+luy/dUoADj4mQ0pEFt1QgZgAMACACNgCAXvAVkEdThJwstAHATnRrqwF8J3pIbARMhKSYiTiS6TC3htcFaO2mhNNbrAEYsIlgwwarDpVpm+a7wQc2FbyfXR1Z8VGiZLYg7gnBwI1+h78RkCKVvvHY8jgy5R8beB5bCPACABJraJMDGHdLlnSy7XOEaj3lM2yKIO9AyWJ6aR0lYUhVGBAu5tww0egADDUAppD2ITAA+3DQH/L8AQoC4FzBpDokjWUAhu5k7JKKzRgd/zyeogvoht4iK96vkvkmJiAq76rSA8DJAT+bmei+ZsZfjJNuDzIjNGHReDcQ6oQMzCuyFFud57rpYgbAL4KnFlukSaw4DyzBCMatJeM4P8//MsnMXmxRMPg8ACCO4yEHKQZBlEjI3G14zkYs2twc8JVMvAbYaQdLZVJbgoD0lgopjwHYhqXUAJoAACXllTdNIznVPLQkd7dwo3lrxgUCUSTteFZ4NpQ84fvD7/Dpk4v04UcfpVkw6bCu4PssHSUYVXTJzfMFTTqqxIDXhYQZ5ztoWHBMEyKwJIOoTJi9YBfrtDsGGwz0XKecrCfzdQDVaq5zRHn90clJmkzgTzUSIB5SbVgRYM2YAfBcr2mdINm6wD9eK8F+eRa6IAGAH+vi9PyCzTbAUoZH1Ok1MHn1DATcy+IA9wlrFRovZfDXfrLRzRufgURJXbb1op8OGyJFYwJ2c2vGkgHAvK/BJuH8gkkv1gkeE+sU/UF39x4lXo3naF1qhJfo/tfnAwBzofLA/odnXTJgMa/bwKu+vQQZ6usoz89SsdKrUeuBE4SsHcrrhL4g5tYer8B9AGCriFgwXRbx7N1tsWbMNx4s9f3bDwA2pvo6vyb+CQZBmM637sEzgL/yfZhHOWF3AlECCHuSmwx87wBw+kXpi6f7Hr83YF4AgPsSvR1T9EOSmUqCXgOAOQ4Nz9z2nmYG4/4xrPOuRr2tGwI4B7YiRhuYxSisiKkvxnDD3G6pgQjehSdzsORdvNg3N5X86/0GysfwHQObubuhFcKbr91KX/nKV9Kt68d83+XySe4UTMWG97tgcm1S090UcwKAl77bcY332YgDgmFmAHCbVNgoGUKW/HF8xlqSgYKQbjZ7MQBarb9iKqo5AJpaYY1akaHTTVcXlwR6OmxsBoaTpJvDASSNq7S0lzG8jmOf4TwdTCJuBGDWS2DyiQHeS2dnZ+n62c2Y9zF/4jGbdYgmaZJ2K47AeYqpGM+hKyuVrhvBBSAEABD3YZmbXMHTWrJhNkmjz2+Xlhza1wtQoWg6A2AEAM6Mnd2vEFToc7EOG/ABow3HcbymGKCJDxxn7wNpcL8MiAl8FdjjddaArJZFW2FFcz82q2sYV+XaXP/b48rxmvchvM9KChMwvBaYgdWsM8U1IY/Zbti0BWkd4v4TNJM7PUmz2TTyzgJQKKwyfLdRmKSaaTjivvf+x0/EsFwr/v6Dt99Of/i1r6VhD+MHdjVRyFqKEEHNrFa6+G8DjDOuih9zHhEyd/sVI57XAFSOAWsWeUiq6ZUly/SUjDyE82WNfGElb0AyGRHzyyvQL3z/0XDA8XN+fpl+9rOfp9VGTUHYxRjxPQA6zFHYwyBHjIUOBeN9r3I95uccexT7RPm5TUhPuhHz+DkuEA+CkRwA4LY3IEPv7MZ1Fu6R/+O6MM737Qv+jhoErAHAdXg2AgBE/AUAEEAgYlzchxoARH7KHOQZACAfl/GEzGysN4gYFdXGsQ8ArHEBzw/Oi+zN1G5i6uu3NcKzmmCVMUe97x22JotvCE/XDNwfINjxnlQxA373eQBAjf+w3Srifv66kQA3ibYWlhign5MBWA5KJil7h/eeXzYupNVE32WTPPOQtfSsqApp+dgfAO4DAMub7U/VwBEmQpsBqHfmSk2WmsTANSMtrx7BoNthANaBZ/uqP7e2Pd6YA4f6OUbGkz28DjAP6gC4SIV4Yl6A9z2bcqLVf/d0zut7vAGnzUn0BRiA+yb6s5IXPucAQBd+Dq4AB9Oqe3SNEosfffhx+s53vpM+/PRJGiDRXINaPUy9pZI+mwWbAbieX7HiPOhvyABE5QWb/mIZniLo0stKZwDQIYG/vJKnlM33yZ4rgJmaAQhT6db4zlXI5knJtFcy3x6AB0iCsfQBEOtA1qpGDgYacf/NCDTDwH5gfq8lctholXiHn80Wkj4EjPKoA8ihpgeSNGNDQWWImzebn1RJcizIAMAAcsnEGsGeknow8rIvJK0DItgrmoAwGcnjWzIAewDiHmQq9AYNGcK7cDhQd+RY9wBggPGI+3J+Dm+ZuQKSbZcAIK7bHnYK/oJ1Eh5+AAgRQEBaQDAgmEYA5ng+6FoWTSIM0uk42JC7aQw5EtiBCLyKxisIpAxg4v02tcbvUJllJ1p6v6kJiJ9lkxCEV0x0GN5sxSB05byZpxHYRYUQNUNLuDlvDKIEgEVZRJHAKsDHMXScEgAEYxTXbgYgx0IwN8HUZgAfzwEST489fI5BerxwX+r10OfQSGGaeVACZE3lXuPPQZATSn9HBjLCkzJLoqPrMbybdH3evrx+t/cbgn0hWcX8O5pMOH5GE3gkSeIFUBDJ06PHj9P54/P04MEDdj/GnAuiS36mYKZZKuP537AcJZ01mMbzi/NHAsiEKzx2MN61RjoRDaZOMEkyYBGBg9i98gnEWEN3TYw7Vpm5vvTpoYQqMOY7Eho23RlPlLBFoJqtPkIeJmAzmlFEAyLJxjsE/MDihcXA9ALWBO4EqeY7TKz7kLkfk+lK5ks0K4EvjWTDAtKZtBZyYKxV8C8lSM8xLUYs7q3GbMzreLx5PBSB+fRyysZQSKDJAg15Xrn3IPmRxFaFFa1p8OxtxonGbvlzOYbKim4TG61tSu4mTwcAnPwcvc+G52EX0riCKZLDkz3+cZ4n5V6+CwD6r40vKpkyEcA+bz92Aaeef63vLHzfsH+wYRDXPNyjmH8Z0m3Pwx0PQDN7MlXQyUd0tralhXHMCgDcdz3lOlj/vZECm5rWeAjtgJeFt92huG/3+M1+WgL7u16nuqO7AGCbEmjPukYSXQApJXMp2JeY87bK8H0wG6INjlQJvUHjAvgrx6K9gtFMiHFXMLD8HoGAYtA3Yyc6yFMmLYCKss6Yfy5U4v0AnDiGgvHj+EHdb7uU+uO/J8Mtu2m+885b6Y3XX0/LNSwPrgiQ8X5GQQxSWO5rHQA4DRjerKd5w8j7D64h41RFM45y7GuviTFDwExAFJugYM+NwwIX0Np5pUIM7CYQfy7k/QXmOe7DdLbM1i+IE/vxoME8x/vAbkbcmpttUbXYyHrn22gyAqBntWbTBMVGYXGxRnMN7QHcx8MP12xMKDK0gTbzFuft/eVyeh7NUwCmNFJWjEvEr1BE+P56fCFuxOf532i653son8Q4fyoABgS4Ls6fyLM6mm70w6MW99X7q9c/PsPYQ0qGdRkH5lUw9jQzu3ipBQCo5ymvXn4+7Cq6ocyCl2L5vV4H8tzKzdHCKqnOc90Mi400mvXR52frIKzjKGrmZpcBkroZBtYPSGGPj8eMi2fLiN+qLsC+HgPcUsCsmHdg3/vwPpqCgUSgIvxkcpT+/M//LB2Nxc4d9OM8A/DqQ6lUxCe+f9kaJPKdsoiF8YCxwOZc0wt9Hp7o0fXbACCOhUI8FUgGnCP3Wa2upPJBV2DsL9EEEXGpu5bjOAAAAVijCdivfv1rOPtw3iyWsHqBF2gM79iXlsj7yMRVrIM8h883Wxc04FcLAAw7h3Iccd0Kxqy7dVvKCxEy70FHSqDUQyPKG+nOSzcZZ6IQgecyRAFgTwErN1UsQG7ee+IezTiC1JkKj16H+crLr7xCiyU8Y6wnvSjQYZyRwRwAoK27DP/k+ZLrhcFgd2B9oAmV9zjvb/VxmvGovyDe4XP3cQucodwvy/lbrr8gtrRekIofYCUy/yq20yYHK3LeiLNz1+D20Z/7UwMAxp5Q4Tw1sJ+ZgI73nwcAIiFzZaFcCJ91Zi8CAJpx1SCsDgw+HwCYkeIiQmJwXVUsTXn1eWcK/h4GYH5PxQQsAzsHRE0TjC8GAEIb7wWeA7NCaOt4/kUBwNIMv/UcQ+q1rxJfPtvfBQDkdR0ygY4v2QcAcpzFzPHkyp4EFQ3aXlflOfM57Vkw9i10AAAJLFmKTFPPDYFNyrQmJ+ni8jL9ux/9JH33u99ND88XDD7H6ZiBx6SjjrQIbAgqbOEhs0oAAAGK/eE7b7KrHDxGECgCAJSRvaUDBiDU7epiKrNnBE1sPvCCAKAZgF5QXDWQzA3/D4bAQIEtAER2BUV30/Dow8ZG2ch0ys2DSXNIPG3E7QRW/mNqKoBNGN3tWAGLRN8SYHhj4XcAAsAkROKN+5rNVV3xwdZIU2YHtjpfmGHjvqK7qgM9AinjYVQig3kTXn+lN4sCNiUICtLAJHQFTgkwrg9BKph0PHcEJ2Q8iqXITX2B79ZI20eP1++jSUN8JrETsgBArZ/qomYAsOwoh+91YAPvPtxbBOqUVkP6iEB6fJwGERRq3GqdNACIa8DzI/0/ABT8ndfMeyPAExs02BCd7lFURJtOdrpCd1cTIwCSYgEnWuc4HmgGLckr/Yvie8qNTs1cJIPV+oYKv9iPpVcaAUCMobUqrai8I4AHAEgAB6D1Zh3V1QbYdJfb1tpcdLkr14USSOe5Msh9NgDYSMbCZD/8kuBNxeedAcBgVgWjE+NHEjWbQavTNzw1sX5AHotnhUCIYOhWwXAjgd8SAHzvV78K6bg8ojSfECQDtEKXbp0HKu0e2yWQL9nxNnvMYh4wYewPc7MQP+8SnMBQ5NoT3k0a27pX9iTKAODRUfbbBPODyR7kdkjkJkcBzB3xGaLyLfBV54V5USaUGBdYe9QpepZZgKrYbzlvPK51vfDu1D0B6Hh6esx/457q/MZKJsM6gNcRLD/e9/CnNGvQXqQMajlmA0Aq9rGW19IaDIkp/abAAMQx29YNjYejx6JloNovaiZAk9xrPhnAcoGhHRchfioTu7qZWPncNF71ctMTBMTPYgCW82ffv58FAOq75Qn3IgBgec771lnvaZSwobt0dIAkeBNMMc/1WtHQSCvjfmKtiCZr7fjAQH4oGz4nAFiuQ/W99/0r51nThbaR3pb32UlJHc/udOmND9UxzqEEye/bx1Bos6XFnvLzq6+JXr3uDArGe8x/zDetUxEXB/PJ49nrbsOE0wWU3U73jzfsWw2T0n7CXAODCbhXLtWRV5+Z87Rn6WNPaZh3nBcBJKPpE85/0h8ywR900dRjkLZzFXxff+Ne+upXv5peff0Wf15YIkhsqOm62wmXrZKZWMbhLsA7vsoFgQAAsbwDcDj0goedblwwlaNbuXN6FiFz/CMGO73zVmqA1h9OIi5AQ4kuGSVUd4D9N5RfIQEre0zT+iLYRZh3Hcgcw094epmenE+1RxNsXFDJgvXOahLfVxwT93oZwCqUFXg2VJAEmIt7cuulm7ISigLwNpjZ2p+2lBg380od7HEc7EHyOmsDfhk4i2uAZPsSnsSXFzyeAN9OgjSStzXW37qLqQHAnH9W3sSeQ5ZKe9X2dRvEzTFpeHxa4QEAkLFhNL7xPWHTvYLpj3MsGd2pZpAjxml1KUacEmsf5kNmxdsrONa7+A4U3LnvbhXXo+B3fHJCIIg+b1NZlFCz3XpFsctNAzt9AkcfP5CkG3EnGaPrdfoX/+Iv041rsLqBMiLxe6Q4gSdgw6Ddd3w048Cr3MMcK+K6AQBifUecxXEcXbZzrASyAov1AeDHWJ7NLwh4quu0AEAVptUN2GvbBE2A0GxsOksffPAhJSwkT1iiHts3gGqsI3OyC+EIqK7IjbVO5GUFcYEXVjH/6vV6Ee83wQmAItfjaDaGJh3cQwejdO30Wrp39yWB410RCwbR1KQcx/haF+ION9eI590Zyoqpp/zpzr17ObblXAvvbACAKnBE3htA1SEAsF7vdpuKtv1+a5ykBgRd8K8tyIwzHF5f22vvIQAwH79QdO17fjvfUyghSpXDwQW/+kNz/2Kg5QKacaUAPJF5ch+3VUwAz//6r/6aV+gFhhtJgWg2mmQhxTXDbV8AUX7+eUzAsotb+9ralaFmla8quhXlvdwMfF08nwPIaN310p+pj+Ofay8GI8r575Wn3U5ABsS3Sibq7yoBVN/LQ0BgDiA8TnMTlfhF+V0Fsy6bfh70HNJZ7QKA1UIfJun5GqpYxed/iAp7cIGpzusQAFgzBfPYO1Ax2L3XATAVz03ME3XLXE8m6bPP7qf/6d9/N7377t+nxZWqf7eHZwxwhgEAggFIqcJWTSDSZkYA6U/+6FtcGCH9BZMFlWYm8gaKstea2G4X0wXBHoMqZXXBCTPHR1UZ8QaQvcDi/iEwZcIZnmVyoUuURtDTbN0jAIjgFi8GKJsNDaUhsRNrz9X1XVAew8tJmruBaQ4JFOQmw2qQvHTQEAUVIiT6T5+ec+PkdbE7p7qX4uXnDQ8Y/B6ehJQiQtLIoCgWtpUYgtwrg1kn9o6O2WILRtdXAm3xgpeQP8/7EV38LCOhzxklR+7GXIN/TsxD+hznAhCLTToIJGzSNiph3egOLBnxkl0E/TwVlASjKXzO8L0ElNcwKe+nwchNUCIAHo50jVFlV9MJMQDti5jXpvAA0vMJGWJ0gd7dcARgqdgNCaqYk3g+mRbPwr0YZWUH53LMglGVAzJWuMXKsEQSKQcYBgKZIb+UB2aeJ0lSCo/B0sgc5zyPwC2vkw7YQ2pdV/VKFqDGexsArAOC7NlSsB45n9z1C8+LsioFoZxbBEYbRh1/15NECV0MsR6cHJ+oKctmQWAZ14efKf/ug+Uy4vz44P33ySwDAw7HBRDLgD7GMABINc2Qt6T377zuRkCrTrtbBqhMktgFUvNISbn+ndlxkWiso4TdJB3h+RhNX9D122wNf1YJp4BhgngBCkBC9/+296a9kmXXldiJOd6QU2VNmVWs4lCkOGloqqHuDwbchtv/zTB6kNXdgv+F4Q/+0nALhkS1CEE0G23K4iRRpEhWZbEqx/dezBHGWmuvc849EZGvslgcZDgIIuu9F3Hj3jPss/faa6998+YNNuDBezCXYMqAgZebOgToaF1KB0jUtIwO5WbtwV6C2YLvgQOPdXbr/IyODtY/7FoteE+GYkgESO9zRUaxAE1pAVOnJsaM2p8RWGMf06ZgCMkUjACIzY9mbDTE0uS5tILqF7WSwi54HbdBHNdiwwDk+RvAeZPP3NuuZhc6w+s32A5me2fGWwR4iBd1L/ue2kcBs/btRvc32c6AkXqgAUXrF9Tj1F67ZlLxjGCJu+ZNuk2SsCDodKDEmGPcfKE12WQTCxPWfo+B6vz3qoy3fEZ32o5X+/f6eQwCZv+l8VdqG2rGendsgvnf+FtZXD6+jJ/t+PPdDzjJmM+Ihvnp9WNtLTcdaNeXS+6wB9UFUv4EfaEA1gREaJ0dXVvX+KPoMl9rr7lSgqB8tcc89nWC2OMnRrmahHV1P1USpnJJ3eN4oIQaA1icg5FAOz1BwuYk/fZXP0eZgdOzicD/aB6i78cchd8Q3V3F1CsMEGnKquJF7y+BHM/EI7VUOa5gk47Svbu8X9exHpebEQF0gF27nM3VNGSpRnNOgE7HalYCnwssdWuZggnGOe6hCQL0YZTgWSc12TDwNuwBuJJth/+4uFTFi4E/fDckWXBd+CdohsIurlv7mbqutYbRfAw+EJiC+F6sK5yRo9EpE6TwJeU7dpnVSLSpLNSSCtXZHMlQzNdiNifzD4AnrjOI7su5OUHdnbtmSkXi1fFXsaFdKQ7HehkABIMs7C01oZsuwPAbuI6t7e2zI6+ZAJ9yYxEAa0qEdWw4OwTL/+VaDECqvEd3hMqOvFfCz1f8WbRxCQCClY+u0ANJbWCg8C+AfCVoBTD4FCnxiCyFuzB/+ASdrNdpNzrnmnn25FH6gz/4Z2ySCD97Mt7xDB1nzeXoXh0akcUe65sI7Pksq+ycWX5YB/g7Et/0gYLZ6QTYbiMNwPVa69T+xuzyKTUh4fbTf6dGM2ClAgDSPgz7CXrW0CF+8OBB2mH9w+Zu5P+ACYh97S7AWI3U8t5G05HFMgO01gB0/FIzp3Oziwb8tWYOtPWomRx+PQA5vNzkLw2njL0+9cZ9SVi5RDfi3ny2VMeDWdW0300FrolKIEgQ8B/0aGtee+PNrLHOxMBcQDoZgNH0TN/lBFwTUx6w/04YayXp5fv1+WZ8pD1X9th5DU4CZujzXpmAdIAlyXGJgam/1/Ycf28Zm/Yx2+/8OOAf92++fZ+rYe860iolHqk1FRnf1QCgRrY4grypCjj7TQUADzle9SLpPld5Pm32ZmVXi2vvGgc2Agxr/XJte7tQ8TPuswUM22ylDZDv8joA0N2Ts19rSqlv4BcAAOlYx+N54RSNnPgCM5vsfP0aAcDOOrgGACxzJgMELZQ8ZCHeigzx5uws/e3f/m36X//PP6fO03KhjOsrk5cYQA53oQGI7mh0FAVIjUdbUq4/++m3eBCvlsqMwserxZVRm08Hpj+kQ/bsAu8pbCOvm7yeKv0SGqdcWm7grJsJsCg+AhoF5qD6Q4tCVxxEqUFt6NS9Ts57ZgNUOlhksOXMvq7jzDmOSDzfcISSD+lxyUGQdhrGFA6dHVKI5+bSjaqbqQPuEZmKaFghABAAEuZZGoQQzS0lIAyWSfOXzsShYNLUe+9tHtQBYuGAX26KI8B1Ec1ZcLjR4QkNQ+5l6rUUQIQZrnAMsTbUzVX3g6YmBJbXAgRxr/g+OBB0uILh1C0BDNZBODYYt9F4SsZTfrbRWHPRlwagmVeeN3Y+jsCL4z+BgwLARKzG1RIlnUWIvDBuSomWAcCY6cz+0wLS++iENYkNNtJdxgAAIABJREFUgYJyDrOTFgAgnZ3oGixNu7jOVuO2gk5kv5cm0bWa89XHfUOwuWJNQSOoClozO/QFAMB6nbQAoO2fgYzczZoOaZ8ZVABIKCWidkyIkEMLUACqADaWoJLJsM4lsZzHcNTwfLQ3bvKC0qTZjBnih48epocfPlT5c+x/OHosKzaDPAJWAMhm3bTOBtdBlBgZAKQtoFOp2S2ATdgHNg+yuHopLRaLuZyf0kcMnRFuKjmk2DcMdGJ9IimCwJljxsB3lJvQEHS37YvzBGseDFhrUNblvGTl4m/sqKh9a0YwxgDOKYB2rs9YmyhJ0r0iYBHDAkAAxLpdSorvdMMUMHq5v2hX0B3UgaTGZ7lYpquZgD8yFy8vGRDkct/oamn7bbuT90xTPlK0YPSJcqYdTqX6e7x/sqZ2/YXVf+emI/G925WZv9elao9c8Dm/9lqSnYgStcaRPgQAZnCjufYhANBN3/BWSl2YAR6SGq2otpum+DtKUwYzXyKZlMe+BCg+P/3ZFlw9NBTXPYvtjZmUnu82cDGDvVzvOABYr7H2nmrgmedqVeLVMqS9/rjP3bUymuz5uh4DldepQybF/AMA9LlWg3/1Pfl5C6B1eB3msQ67a8DPvoqZpi0I2DbHwncjMEclAoGMYNn6/st6ifNorXVrQGjSCyBwoJLhl++epS9+8bfS/TdeE4NtBwYwznZrD+o74Bk6gdZhAwaBofTPii7iToReI6aUNWdDO3g0sN+jcXQSECDX4ydP0uNHT2mnsG9w7o8ntwn0wVZjDkZsVhVnAvzZYLDjHMQzrc3LCOCoP9a84wVtsfEwnjcSJijhpK8U+94Bu0DWbRqc3kiXFxfpyeNL2lFIsmhNyK/apZW63EfzttFoypLm6eRGJMxC2iGap/GZITGymmcNYTWNigQZbPZqlTVkV3OdGxbrz4y/ANBy04dK0kl2IPZfJQkgP06AR5toLJVQ0agoGuMZAMxAorXuKgCQ13McVgF92lO2W10meastizjVwCPXfFwwP3cAR96HBgBdkw5/m03xVmq61x8JAESTL+w9+wNmKNIvj+7ktM1bSYfM12qOAQCQTbwefpB+53d+J7396Td45k7GYgCiFB1AVS/iE1RI6N7sb1p7uQsAZt/HPvUWDEaw9yTbo6Yg0vrke9nnQ5UZtkWsZJpdEAAcjpWwbgFA2zAy6Zar9PjJYzYCW21kU6EhjIQ9SvSJm3Bf9nPXXo3TNqWlElaOBwyUW48wA8vWfg/mqr8fmsq091Faa41i7FPcxyK0MnvjE+5zAIDY09PxiPM5BIO3An5r6STaSldmNRlIA4CIOZiw7yvZ8Or9NyT/Ap1BXHcpQgMAQMW5RQOV++5ASa33Tz5jomKkPjdqAJDrywzlGgTe860qyQvjAwcAwL2z9zkNQgAA7oF/nBBpTtbw5iGQr05m1c/3Uf+7BgBryZqWGJW1aX3QWCP6j/+NGID5gI8bzzfQiDTXXW4xCdZqqm/4GCCng7D7yohk/nWX+bfnQLUIcQa8IpPRIKKtY7GrAU12cWpquuM+jjluexPTZm7brrbNB1rAkGNYba698XFweyQz6o2Yuwu7m1R8zg52+zxemL1ofpFFhxtm2XEAMK5QBf12cGSQ9ODtWmgzmi2g2DJMPXwfhQH4cQBAG2IzffA9DAQH0rX7cLdL3/ov30p/8q1vp8vLq7RdAIwapFsrdXebDlDuuuXzArTYbi550J2dDpgZfuP+q6KeR3t5OGjcBzhEo0sZAZbBmEyfy9kqHMl9YFp2xaXD+tcZiGMMwFySmruYxQEaTUCwHhFgA7DC/IE14y65WDMCitSh1Ou01qQrXe4017XWkJg0vSzKDKYQDnB2jYL+BfUBUdKng9lOBNdNZG7hkKt0RzpfCNj13CpNQPe6OiCjCEf1MliEYJ/XbewMrofrsBxos02L5SwzlgjwgL0YgY0AjnL/AkDEXvPY+BngAMDp3lDQGpp/YoYi08xgJm4EmbtDAGBe7+iyEEkJMhcmYFcURpGp/rgH/B3NHvh+BBx4Njga1lNB8wlqIOEZpEG4XhWg1EGWn1P7FyXA0KCzA4YmJYUViuvbmffnyW6BxhAcn606ONcAoMorNU/e/1mzL0pb4cCTxYXOv2DExrytg0FJwHkL8K1Mdu10Wzy5MNz0Pr8nU/Zb7S+UipHpvh+ICtCQlp+vNR0N2R0XjhCcOGgguSkFvgsMSAPSnJcdmJyRJQYIjACHzhJKl0Z5rCByX2toPn78KD18+CjNLy+5XzwfEHEm+EMtEjCQI2MdJVYEznC+VGcMQeqeSve5ditQpnQ7jVKSKN3w8+JfggZR1rUEy7XSthOrUNdkCXewQ8CQtvYW1iCYG2i0Aw0brbNgHFRrA78nOyQSHQQsYu1Jn+oiWSPJAaP1NOuuyCy5cbMIBPODAUHIk9PTTlNKfA91JaNrM+w/ui9r3uIss1ZQsAXnAQC6EzqYAHCsFWRIH9XjUYMdeT9cAwBWq/sgw6wAgNoIe5qYudxLAUdpJhAAweIw+69jRJ/7Q+F81G/LYF3IR2QgPTa6gZY6APA6POR7SRu4AeNi3Wh81Y1WDQ5cCszOB7qtVlM4BqLt0ld2fVfTtNi2sv+5bq+hZh56lpJkKVInhwBAJxB4/1VppB7oMADYTlXbZKoGADksR5gNe1Nes9Zqe2EAIs6YmuVZA8AAHA6xTPP3ROVRuZ9giFQMZd1vMJeilLV+BoOAGq5iox3gWmoAdgmNigACDkdgB6ncnzIATrAGALNeia0OSRg2P9rJd5mMg7Gd5ulTn/pUuvfay+mll+6k23fQHG6YFgtp1ZZcVQEwOn6ypSS8THME7uc/7AfaljjJMxyoyRv2FZNQ8UxoTsDE8tOZAvWd1i8SHhjrkzv3aAthL5EsUVMnsKqDYRXVKKwgQaLIciI7MMLWab2DNvKQ0iYsvQ0AENsc9tXxjue+tk+Yr2Vf56alTIcG1KIqYDAQYODmck6YO5E9my3EfI8S4RLHRPIaDEIAMhvJhyznz3jfZCCiocMumnplwDWkWqJ7sAHADEDnfSgAtY5f8pxEJYfXNhmWFfPVgI8ZgDWwlZnLTJgXwMuJITeJygleN6vKGynWTfxcM4CtoeizFv9ag1w2tJzdBgDxeSQqsZ6hhT1bgDW6ioR8sceWPPFtYP5ZuRPazgDGkGjb9k8l4TE5j0qji/TFL30pffXLX5DUzDB8Q/rcYLRJ4seMyMK4DAAQXYYDbOb4R/UP9oHGWYBs/nmDBh1OyG8TGICMWyCzE74Gfl4sL1kCDE1Cxj+oVsD3RNds1VzAIK0JAD569IQEkcVSDN/NestYyhp96+hWbGDODNrxTs0Ya5+Yl22Z3ex07NLjAvZiLlSibEAz9i0TNv20G6qEvz8WgQEAIOblhADgjNIGtI/V95WzLsqSa3sf54BxB8Rl8PHWTIwM0uv330g3btyQPxx2E8+DLt5dlrYTgofxl+wrHcARWvAP129L448y/7wvXCBZJeDr/bp3/lW/8NnC7z0CAFqzvvYvDrL8DuBZx77bNrT2H3w6FOKDmYDu3h7nYPadunFwrwYA9cVN4PMbDgCW2vDnA4BZayWnnJWhuA4AzIb/6Kx0KawtA7D+GDdiBRji2rlsyhur+Z58qBxr0uGSTiPpu66WjJkT7e0fAgAPAWi/bgCwpdDXgR0NZQ4oWuphQy0+Mn81AMhDkOVEmwTKNALAv3n/QfrGN76R/suP3pWhXQ5Y2jt+Kir8YHMhMA9qYGuU8s2ZAbn70nm6f+8eGToY1zVEcwGwocSUWRyVdAIw08E3SQ8fPUpXsyUdU5TeGSSpnfQWAHSJngHAWgPQ+xmBkwMVMeBwkDnDra5tcP5wH86MW6QZ74XjhADXgAH+Gy/8vFoXMKo2xHm4d6DIL+k8qlkIRPrVyIOlkjvQ5XWwGqzhnEfphQFMB4xOVACLI9DUObhwoLvDVwCl0DCL8lWMY3ZAnQEZ9qW5CHYdS5VXaTyZpLPTU1lD6nloDDBOCLzMHMB6IHuOdiREfVGiTI0xZJpVYsLDfY1SzyWZRASGzERyl8OGAYi/436hcUeHnc0XxAD0POD+cPDSroSot4EeMC4BCLokzCy/omkHnUAIaysQoBMUeioOqLm/emKmWayYQEvd/S4/e2FiWuNMnpdYn7o+ujBLAsE6bpnBwOdEml6A82ql0iKDZUPox0GvJZekak5Go24GjnNWie8q4K3YvS4XccBaAYAde9I2ccoLOtiMoV0HxhnWCtYEg4wlmr64pFkOPJ87SktRAsbu06FnuEOAQvkAZIwBAIpxBwYN5gHOGuwQnhUO5qMPHqq5DEqOwSCNAKmIYgejy4kZa0KFFmB2QqAvmVl2WrNcBxEE4HHJrEODEpSWuGlMAErqXiw3mN3myAqW0+EmF7genom/A0sA9xD3c+P8BkHAyfmNvP4wTwLgSvDrhhru8uomAxgP6O4ZGBkMpUNJJzOapgjQQ3nxOpegcWxHI343ypHnCwSC6jiIwBd7FNcluxtC9m56MhzRPthdZck1bONqxdJfOsHRMRwBtxuRILj2OHfO1yo51v19V4KlHFt27IrBM/jHuUJ78yMAoPZrdFkOINMatKv54W6Ex9yd9veMzSpRcP+9ADWRsLGn2kixXAcA+jrosn0IACz3E0zqYNag6RTPZWcIjgCAGSCPC9UAIM1X01m7TQA4IXVsvDpndwVg6tqWWMAgFn+lZupkZ/9jAoDt/bl5l+/X7FzZ+q4cgp+f7zUQ3gCGSPDlZErTpZA2jzrGqlR4HgCY/fN8H20Cpsh+cNwSyvZr5rrvXeexbrn7PLDLknfpsxzu9PQs9QE4BBvNbEX+vDUAjE7ka7RA5XOMB2hmNkxu9jZI85B0mKQ33rifvvBbn0m3b9/m+UVmXO7GHVqt+bwpXYA5x/m8icC48WvruanthcsvqV283aTZ7JKJ6jnkZli2GcDXIuxHlCQDACQzf3yLCSxoVFNiBH3OmFyDtpuTM2pa4EoD+jfw2JBUGcAHGqTNStI256ey52iugHMNwJ7ng+dEMIHICgLjfXSqdRFakgOUeAN0YsNi+IeR4A27DuAP+wOaamRmWUInJ1ILgxBrYLMTAxy+GxLsi6unqtShnwZWflj0aAIDRjATVRudG/DXO2BC7EMz2IMQ3tn+mh83kdKf7JfLD9JZScmgzCBsiC8DzU9d8ipb1C31hf9cAwveR44TVEZaVb2haVLlAxlQq/1Dzl+kynGmoakXGHJPHj9Jl9GEbTTS3kOVAl7wU1W6Kj8QwB/9VOh5j6Cjrb0IBiDO7smNO/w8ugl/4Qu/lf7ZH/wT/n042JBJCj+ee5gA7XEAEP6vm3LQXoV2p0t8oSGtvRMJr/VcGr3sGL1Nm1UAiQ0AuFypYgv7HD4B1net0Q3/C3YA7wOB4umTi/Tw4cM0X8pfh+Y5fMK5JRCiOsKlwKoQ2aVhMIxdBeNqpGHjl1qTm2ugIt3Apun+EKOhS3FoDELuBP4WEsnwh0doEDgkAIj9KABwnsax/HPTj6wJWWynFzeey3GkAcA8rtHc5LV79xMar8GXgjTKNKST4J2YFKDrxfw2TbXqjUTs5TcQAOzcY5XRqM/6FgDs7NGqaq1NTB7rNtyenTnBGjdTA4C2P/WZXuarAQD/w7/9XzjEDgKL4QonPPslziw0B/M1GdDW6e1YyooBUn7fNYSuca+daIFyQSU90h67HKglg5ZR5Y6Ww/7f63vMzULaG4+fW1FJG06/3RsmM1yc8Q+D6IBLQbcACjudNfjYZvZ9/UGIAB8b5/oaHMPWAa8BlMqw4FzlfeS29A4QsznQNg6gJjMN4z+6wEzFDIsQ6tjz5HGL6xpB9/MZAMyGoUmQ7nU93isFboFBBKUAo5bqdpbmNF6b0Rlv5Zs/eDf9xV/8RfrZh9KqYyt3CM4PAfit0zRK7mGA8Rr3V+ne/Xvpzi2J/A+SNL4Q+MMAwmmCYSb7j0BaNFtAEpQOqrpE5e57reZDABwep+UFmiSwt2x0x8Q9AiiK0gBqy8hwl3W1zUAYNFtIyY/sEu4HYvZwTHCgMzsMbbYQ/eXhBrFva9fl7rAqvSAUGqCQGz9o7gRg1YEEM9ZbUezpyLJLapTPWMx0s85aPWQO9nWwWpNvGswrOYNwUlVa6+/Oq7Xq/MffBXMYMw6RYLwwt9b7sz30v8iWIbDHuFDTkeK6AGi8gUowwsvvtJ9xCAIIhENMkGAxFwAYwcuEjmIpAW73MfwA66vgumZoFuaJmksAACBDIcq3xZjBvIuNR0w7mkjgOm7aMrtqgFevF3ev6/dz50QHrHo+OXq6dhU2V3qA9bni58L64DyFw57tRj7oFWg5UMX84uXyPgBS3of4fmgGGnz1/Gv/CAjzvfIadISLPqTWRpXFPiK2Ts3JgRgsBoS9h0/GEDmvxj0y0JRmAIM2HGlr0VFDMTLNLo2mfpkjiZ1Ke5FBpb5VMMrZOW+1Tg8ffkhHczVXoLMNB5bAYjTxIeMQ2oAA9SrJAM1HdKBtMpd4LwK2XNpLTS1pG2qYIsGW2X0hE+BS0nhugxc1AMRrE/AWkIjvIZiGZkqnZ1lsnuw+loj0mUGnYxtgqplMuD41JKOUHusagQa0t9QZVIkGvEd2SHsbTGOKlrOb9zDdu/9aANFRIpybweA9y3RxEZINIdp9OhoxEBoP1XAHgSFs1YqgpcrWYR9YDjwTeA2n3smC3PW4YjzWiQ47jLZX/rf2P7ieQ9Re6xo/FxH8/Jmmcx+nzyXeTDiU/YFlx3VUaWe2rFld1wGZfnISoAXM2vt3KbX3XTn39UkHHrXzrOtH5rpTRBNLsQLSOsmG+HLpXarUC8w6BGj8rih/t8aSzsXiANW2t/x6P5FY3+s27G35Xdc/LhpfHhnvo2DwtVSPZgAz+NBoD/ptdQlztncVG69mKHaDkACaYkIoEVKBfPZHC5NuP9HCNdhOePWzwXit1cLo41uiRBslk7LVhbHQXQtNgp0aacHEDrvC9VKNY82QyGspgBtrFU4mIzJVzk+n/H50uZXvEIlXd2V1QBzMahhEjAntOc7GeK7T6YZ+ymc+83L64pe+mM5OxFxaoxsogKTJGRM3AEqYLNohWdxPk8FOPg+uy/UZgClNbrDoWaGg+wLggPdNorvtSf8W7c58uU4//vGP04ePnpDduFwj6bxOy6jZ7Y2m0lANJtLJCTqmn6Xx1MCkngslgUzsBHBm/96SMdvwIw1gwO9EQhcAJBLfPq9pc8LPJbAaPgLXFdZZNKfYxjlgfyihVDWAJMwLn3WChlVDJY6jKWUGIKGSmM911nNm5hnGezG7YjOtVTAy0a0ZZ5AqC+Cvyr/o5eRVrMNsI5XMcyULGHPSj3RzirLg67gtx0U51lTiF+tdACCYW9u0XZoZKyZiD8xWylJIo7sw/5TQyMkBa71bCzv7OqFhSW28bQKQJBuiOAfPYf+IlS9xVuC5tOfNVPVzQb5EXaah6fj0EsmxonGL88/jY4Ya/eIAsExwwL7C75erQTQvO+ecYh/cfelu+u//h/+W9vlkKvmNNRqBQA855qfVHnUcC6ti/9l2BLbfcwG7QMJCNEfBPFP7N84EN8vDOQS/Cee4z0s852z+lD9vtpAckv+oxjqQ5xmn9dUV7cCPf/qT9OGHD3ke47ux37HOF1F5sIx7ys0pbGsrv682pfW54T2j+QGDt04WaW8h/mQ8acARzfbw3wMw8dQt+9bt2+mVV15SAnp5pcaPTE4LsPVL3xcVZvFLxEjGJ7RhovQmSqp3Q3VDHp/eSp/73OfEyAfJJfzXsXXkmchTwov+WaVt6XHn9Y/hOlUzx3q8cnxxpPIxn+1txWaDH7S4RT5TGwZ8qxzn95WmQTWmVhiddTKB0kDx/QXg6/oHdZL38FHrtXCcuNf5XPP8PQOA5U2+UPzbNs9oGYLXqVMfKAOtb2hPUy5rIMSDBQBVAEpt+BcFAPcCa9e2H2bYV5vheS4OJrYKIOHINNdrAUA5Q6BvxBsNsAUIawCwnexfBACUg2TKsg12sDXaRGt8cT7wm5rxUkoWmf0GAJSYcS8HHDbEuQToBQHAPbjOjqQBg08QAMQmXPbmDL4X/SkN/V//w6P09a//WfrRexfSqAjdGLhScCDO0cQAWZj5FUfudLJLb775Zrp540Ri8uuZtE4QUAdYYoBMzQ484M62qqTSwEQulTygZ4ADfTsPIByUdwQ8fQGAKG3RSaZ/2vVj5iNFm9GAAIdBH6L9y/Tee+/xg3CSzk4FZBYNErF0sA7MyMLzsPwUzkRkaDnv252yy+gKOlApp8VvkYUjq3Aw2gMA60wbSvCo9wddRjCRJoW55AMD16JuXDgmtYaX1n4XnPPAcB8O7OCZki6NMAcRYHUa9MV9IHNubTsBDlGiGEBuDj7CLuDzytCp1Hm9nOfyb3z/OJcqHN6IkCwommU9Nm+R5pwPiijjTOp+l+0LO/OCYRolAdAkia5+Du5wYF9crLKIMz8bDrbtLTuzAYiKTr5toFXrKOW9Xq1VN1TI+lKxIA0AwvGgPSczqQB0AiV2mXlI5xdAWYBnfgYw+GvQt6X+k/0VNkPOc5caXzNYDjkUAnmxrqN7dDh0ZhBMg+HWBp05Y5sdbIGlGD/vaX8f12GWbpCYPcHc4TCNIX4ec+3GGHiOpw+fpvfff0BHiSXHFWNW214l+/uZ3WDsBgDuc1GZZwF0XAe5YYPP4WKJMSbSVZLSUA221s+k5i4KlNGtmIBxBM7ULwx7YfYt1isCWzBwYS8KgAtRcp/77hKpUjY7ysvVTAyYORxyJGnMSFWJzXql0nXYaqzZO3duKaDKmk16puFwQjsCbVKyG8K2JGoN4lm26fz8PN2+cc6fkdG3/QHYDzYg/g/Q3/YvBlT/fEwAsDi4FrW/HgDsMD9iYpxAMwjmQP6XBQBmjbnwI48BgB0fJe5V6+v5TH4HQxlYiM8aBCQD141BOoluvbFtCmI/yd/6vO6DBPgrYDxO2tqMVCL//vVxANDfXfuqNQB4aIzqJiZ8nkaH9aMCgN3SzG4nVvoPwVjpPFwAgMd8a9qhve6gcQUDGNGdMzOXGkmCFkD1ehhECWvRMCvnt+8xsyRCAoEBLM5gJBVHAwKA0MLC/eOcFFBl/7xhfwZzlqWNbIoVdnQ5U9IwXdGPeu3Vk/TV3/5qeuvN13nuQ8OMbOLxiZJHacfAexMVD70NgA4kPuTX8DyqWDhM7rGrsEDs8VggjSsmrh5t0o9+9PdpttDfgSfAxj16Im3q1IM8xSgNpvLjoFUL4OLG+U12c11tA/AIPwGSEvxYAIAG/r0+tpZgCX/H3eedeKmZS/Xct+vSzZiWBgbCLqJCygkKrTuNlzs/W5qDgC2BGPk8znlYOgYSIPBToTH3+PFjVmDIV5E/ZHkKjI8MQWg2uhY5+xkBhGyhB62EPUuk3dTlaPxrwLvEXRgDroBo1sX5mju+lP/syhz5Cphr+UWoTKk73QJQw8+jaNpS/B6DX+HnhBSIiyAoexdVKLhuAWz9ffsAIP37oZiGTy6fZJAM6/Dkhhp6Yd2qmy4S3QDSQxN0JCKBGdmr1Sgtlos0Ht7ksK82y3T7zu30L//lvyDZYDIOZig0oAFIf0wAkFMa/h6TgTHvWAfwCd2EDmc99idKh/GCPqTjVyb1AgBcb6QRqFJijK1KfHuM4bbphz/6ESsHdkndjvsDJRZS+IeU6olu2rQDAQiOzfQ7QFSpMY9i07ols5YYUQkwOi2GpFBI86wikEeCYHpykl5++Y7sz1L2agSiQgMAcv+HVh8j0WqNZwZgnMsDNk3cpN5oTFB1NL2ZPvvZz+ZzD/YE62Rk5u/HBACF+XSlbDzHHpva92+lz35ZAODe2Ze7hndPypqw1CkFbrt2N/7O89EnWr74oo8JAP7Rv/njXV3SVTK9hwHAfS2VaxC07jjkwy1PWn6ArqNnQ7ifQW0yrE3tev113EDp+SUuNWCXQTJepDHgeQc2A503blczxu/K5QjNffZcYlABgPzWI8N5FAAM5P6Qo+oN0nmulgEYN3pdl+EMCOYb6S68jJw3AGAGsHKxeoiiGv90LX4LnObuWxp4f1tbAtwsr/xjFsG9lgGoA7afVKa67q14wM8HUwq6f/uHH4oB+PCSQMj5yRkPDwCA+NzJDtpDm7SYXTAgPh330lufeivdOJcY8nqp38MxcgbFYv/M3F4uaGCLrptLDIMBkUtDu0+qbN4u7UA5h/eDkoI4mBjIDz2gAQDZAFcMMQKTURKCbp50LDe79MEHH+Qsk/UAMe0WWGdwzQ6/aGuvEkscKsgAAyBjyTDLoXukg0N8FjN4cXFJ3QkDctQXhAZHZNU5PizFKM0T9Fw1a0sZONwPOxjC4WCQLqANoIDvmQdZOJYGjLwfc/dXkBIpfK0DWoe82CMs3URDFxz0USargFIlzMxUQiOnU2qk0hIdoupGSqDXAvVgAwIYsaYYu9WhmYnml40J4rsI8MY0WgMRJW18luiEjEwrAZkBShRdNiJNV+5/6jAiS7/LumdinvXIVHr06HEuf67tRX6GeH5cQ4xVJROwjJQJ7zKQ8Gx1Z8UM/OUuE9EoIhjGBgAF+EHDruvA1p83UFYzmMr6KHbZJfYtC9SlL/VOOqTLUR/qBhYAxNYZbpfII4NuG2dwSwwqBS+0XVXpOecxwDA7bBz3YM7mpj5ck9J0wvrDfnZnaqy7i4tZevDgPWpgwoFFBpjMv4z7R5l+MJb9e5eE26K6VMgAYM0ArM8NMdXdGbpiA0ZpsdeFP+MyJwOAYveVMz5nnHPDI3X5RRAAgA172MA314YZhnn9aZ8AsOO5uUP39DVlFDQPAFBR4tXT/ltrPuCwovz39FQJnlIapIFjCfZ2x0AAzzC/vEoXFxdpefWMf7/1GVUJAAAgAElEQVRxdpJu37mTzk7EMsR4Yh4tkn55dZFmV2IP4jWZSCu2lh6QjQmGdqVd5T2gNXPYsWszwm3XX81vKbesM9qcIydwgyW7BwDaXmZAvnvu+Kd96bjDQJ0BwPz8eyXAXeZBTnxV/kDXL2vup1pTtvEcvwyGCQB253GPLcE7NqsJu1vZcPzGwFIQg/KX1vdCO13Nk8/3+g7L+7MHE1+on/e7ELe6yZFobROdbqoQYvv+TtuznHA9oPHXsW/xxhcFAO3OZv+6ZRZU32sgqGYXWRPQpXq1rbTNtU3hs0XzD3vzYgEqwdEyFzUWRZKCCdcAI/Hs8GNga9CJHUQuJyu9Lwm6NQmSotmr72MyA75GaBRv1pCCAQtwnT7/+c+nf/5Pf4/+ENjE8JOyFht8PbDZyADUpFobledIT91ozf4DyICEAhhnbEIWjL6rqwUTQI/ee0Zm1ugkAJW1dJsfXoA9vU7L6KbaH0u65ubN83T79p1089Y57+/y2SMl4Awc7wQkptA6xfzoJgMUDikIa/KBUaUKhW5nTla8hP4nbZn9VTcNjKZwq9AavE7TkjYdZ00wEKm3XSUDXRHCUkh2GUaX92WaXT4jQxIlxRw/AMAAeQDIwKdZR8O9uN/SPKMwnuHnFDtWzkFr/ea9VwG3nlsn++sSb61vfWo40PmVE4Ab6BWi3FTfP18LqIHfAJ8DgCfOJWgf4vnA3BOzsHyG+6Y2Qs1/KzEU9ieAXL9lnwGo/TGeKIF3Ob9IV5dgoYogMDw/ifO2+IEG1Al4QTuT752GJt+E63Yx3+k60FCeTtN/9y/+m/Tyy3dZgk497ogfBjtddy8R4AqhpgOwmfQ+JwGc4n6gta1mfMtMFECcRJAP5fKQCsJ+DJ+fYz+fp9nVE34/GIAZUAy/AXtzsFrzfd//u79l5VRvMCbAuAl7uATHAD5iECSKJmvgC1kioMyYzn7Nj8rAD73KucDxZcwgCSTa99Ds3EYsAm1qxEu379wUoWOpZi6TrEEqe1pX8nE89qTJuvG7kq27tB4oDh5NbqRPv/1pxgYaTzGdxSzG9SWHY81b2+66woD2yPFRSyBrGIBtwr8dqZrZ6PiB282M2UYuohaF1nxHFZHlLRq/JP89M8U1r6228LHtiJL1+nU94NdeqesntkQEJIoOvrzufpUAYIuW4sZKhvfjAYAdFe9DT3pEO89vPQYAHi39vaYJia/7PABQFsUlmmZ+RabJCaPGcXseAFgv7OfYff0pG86GAdhQXMv3xf3FfbnpRIs81wAgDReqkwjoaCTqAKh2lOvAtLMRrgEAfcAeWlN8TAc6HxEATDsFbKsEDb5pWgxP0qOHj9Kf/OV30ne/+530ZKHSr8loLEMXHuE4KNyb1ZzG+mSc0v1799Otmwr8VkuU0w4kzo+ShtCawrgADHv2GE1DxIxRR86gtIddMJjSzqsBwJ73t8XyzRhrxKRzQBDAkRmA6ErNDDSOyN0unYxPsgOCjBaz3cNhmofDReMGps6JMlxXyxXfj0MFul6DobpCAfiCo3t+dsbPz2bL9OjRQx76CMxxGPFaIzlALgHebOUAc82wlFHMHXwXWX7rRQZU6GiwoUhxaMwyUsa4ABYuo81OdzBRZysDkjL2cr79/WIw4gWnUS9pBtaNUKidAwYlxhYhSaP1JABQpatwQDinzJBu0nYxI/AFB1yOYew3O8w+oPoo/0UeLbYxxpCMPzmkAHCR4TMjD91REXhk4H6T0unZaTo5Qdc2ZWwBAD5+/EQZ9shUGjRzUMIgK0rX/Qxy/sSGbYPzer0iANhbv+FweD3WAKAOfpcoilm5f8Cr5Nggrd7vUt8Ab6PkiEBrpWPENVUf6qFHh98fKo/znuN49QUA0rbQIRfQC6ZBHbyWYKGrxUpJBerhSGtO67sAwQXICUcM5bIAmDdino3H0rxi52ayGsWMXYBtho7T80v+y251cGYjkMtdMiPwau267bTBEzLzLLoeQLQcwX2xZo5tc27Ue5eAW3Tc9XU5/tzXsZsMaKO0hSVAU65Tg+7+16zcrZsZeW2gZJr7Ruvlgw8+lMYNOg2DAbtVoLFaqXM4xvHu3bsJJYDUAoJ9AZMPzBkwBWJ9skwMUhCrVXr69Fl6/PP36OifnkzSS3fupBN8Hu8fSuMRQecVGYAKODFPeD0PAOSai+YHZqZ2df0wSnF+VjqW9VnQAoCHxNjr9/tcdGm4NTW1N4rOXlcUfJ+d/MsAAG1fnw+kdU9CJKIOvTIQHiWVTFYt0TAA5ZRKonzSACDHsC0pygFcFwCExSI43ildNShchwH6HEp0O/PocyFK8Py3NmA5ODjVL3svAADSTrrkPyd0wJxTiaFftUg6z9PwOTpNvlCKSG1k+V21H2cAsD5bwAViQq3JVEOb9hAA2K5PfMysKuxzdQKeHtCQDUmN3FwwnspdxANw9LlyRiAJ55a00ObzR0ww/NPf+2p6553PS8eOTGM150DijpUgCcDZgbUbDp3tMWzW6emJ/ArY0u0wPXr8KP3sJ++lZxfP0vn4Du0bSu9g89B1FFp3D5/N6WcBACQAMh0yGXv//mu8P2ivwb+bTqKLrsvGyUzs5XWJrq+cn0gs+P6z1nAvqYs6mECVtjMAN7yg/YpX0fLSeW0G3Xroda3Et5mBLB+PScznboAalvJo17ZLfmGn4XPh/7tN2OGREqXQZNb8C1wBY0v3GQzSDBzL5k0giwHJivgXTUjwolwPfO5IGtfsPK/lbowigElHJvZElAiGBqGlOTYrNeWzf9nPuJpKcc18tYTNcinmlwFR+yJZA9DAVvgtZU+Fn0m9ywJ0cK7a2JNl1bJNV4srAmgEZNH05eZZSCuUbmw1cIRzFeMxHk0JvPcH55Qx6fcAji/S0wsl1v75H/x+euvttzIA6MSuGYDdklgAwXEu5qYffoZS/isZCAPaIRGERGlUcWA8cQ9Xl5ecT8Zg0bwE8QuA9/lMJcAQs9Qci3mKc5lM0PWan//uD75P7U2UALOp4krjODo95fiuHQubMR4HlDVwPeblX5fmHwYAPR7W1ESXXc6dS+t9DlF/cZNWa90vgP/RaEyNyxYA9D6VP6V4xCe/za6/N/vvrPjapjVJBbs0nt5Mb731thK4+PwaTM7jAKD3sEuOcyKiKQF2bIbvLbIUz4fL6uYm+Xxp7Pqehm8tg1adubkCbo+oVO4B42CLXoDU5xPkPgkAUGfRkQqy6wDAf/uHf7xj0GwGSjCbiNRW4rT5C1zKETPXLs/WeLSHe2u0t2GA8++ztoF+c+h6sdL1BpfSthc+9nMD4OHj9XfgefRzy4CM+2kc8W1o8B0rVTnEANRGCwQ/MwE+HgDox0QV/0d6sVskvisO4moP1Uao+FndGTYAmLtFVtpB9by0moe/KABYntPiyTEfR3S7MlPVSHd+zsMGdbtDqe4uLXdrZkrXo1vpwYMH6X/7j3+Rfvazd9Oip6B9Esy6SWQQx8GUWC7AEByns2mfAOD5md6/ms94UJo6joMQIAG0zOikXCjDJwcA4IZZaEHlPlD6yz0VB1+fbd6BLkYpagMA0tGsympy0wXr3o0mka0Z0IEbh5YDDkQBgNJeoXYWGF8j6KcM0wmAPWR5hmL84UWx3/WWmTBrbTnbDodFAbeYe84Q2TALAEX2GoAfNGicqUWAvaHTwUN6AIdTZTu8Xiw0dFGGw2uNH3+PASgGIi43wbPbzkTTEHcLwxwiMABAUDdrwFgggEFAz+53G2X++gNptXVZgnAu7blJV8TZagA61JJbyZGAA2FNFq3osDsV65HsJzaRGGTNDDbmwPhH8w+zHlESgnWE+xNDSeAymCxqwjKKgETMKIAVLNHG2LE5h4DKmgGIsXNWneM6xBoYMzDBnFkbrwXrakc42xPbvTZQzs8tRigyV7W2YG3bxPCTmDa6FarERE6zAUyBdMiW71tFlwTj/rKA/TUgYE6cuKmG11JuJhLddIMdYofeGmgGHocDgHlaG24WUidwHJBIP6ufbt0+j/1lxqmArMz47kWDmquLdIF9N5tTo9OBlPERgpgBfgqEk0Fknz7ec5RYhBZmHbwIfOt2C/aoblgG3+0kreuWMkIGlBVQw0SIr1c1Y1GDjwklGNDNmo5j6DUxWEfGPRxoJAq47oLZjCYgsFPvPXggOz2ZMGEDkXcxkmFPYNsnFOnH7SAg3+4gQ1DOTjBqfX9MyGzUlR1EFXYD7WlBiUmyTuhWzXMDbJOrq3SJoGE+Z6m674O7upHScGLMjIUCrBRHztqrdSlvu5oL06uca0VPpry79u94/7EWEHDWrwwCNn5SyzzMpbd7PngElnHROsPPNXWwu3b3Hlqfr/65rVQAAMi/H/EDcqAEkLwCAA3MtCXAHouPogGI782aTvHBHJxUiVzuvVZKJd7vLtPVTO0brAoALKXtYphnTeTMUI9TJPzaFlhrL/48ABDv3QMMWgDQido20VrpCdYAYLYbLLV144B6ERkoCZsSzD83X/F4m0HrQgfboOLDdpkRNQDoxieyEY3fXJ0XGjtX9kTpKAHHstcGG1Qd9NN0EjZ9fslk4d2XztJXvvKV9M7n3qa/st3MCAyAGSfDq+sAwFfXcTMZdf6CWU67E6XOk/EtdoH/7t/8Pf2r87PbBIQW8xX91dQbpydPHqeLdY+Jwtl2QJt0fudWeuWVV9JLr8LmDdN8oQZH6G7KJGzIueXEgFl7A42/7YS7z2pNSBMaLzDTmfQCw4rVGEpYoUSa7w0G4SAA4oJ3awwLAzAqC0JaotMUR3WrcS/BzOvLD8V4LhfQartkN9bZfMZ/t2sxAK0pC6CYlSO7jTRco3JhPFJCkT4FvmNdKhCwpgD85SoSnsnSyeUBEuecNQt5noR+odd5SUCqFDK5aVe8wdq/YPYxYRrN//J5EIBw8aUknYFzSueTGnDR/1mjqZvGp9739Z43EcHvqedYH+zGR6piATNVGuMgAsTE8vdgAEp7WuvfO7kUikkrF0AezsXB4JR+ytnpHb7/yVOx5b/8pc+nL33pS2k0lEQHXmySwoQ6FpsZcX4aA4D6marS4ddozeJsL/raqBQyQ8564fgcmoTAB6bsULzI0F0i8fc0rRaXJEjsovs3/I4aANytVmk2u0rf++4PuN4AwGMulmtpik9OTvkcYALS3kU3exMw2mYf2X7tNX3SzZn5nP2zYOD1o/kJmGcsNTdhZyA7AD8JGsY3bpwpERLEFZYAV4CvbVvx92I9BeO6VJDEYIWdvwKZHozok1vsio7v4vkYBItxSAvVCed6Xfq/2V6oaf5Rr+Va0kLzHgkGaxQfYLzvxSWoOIsvhPRC/dqrWGpuskijxR5zUzxX3tQNPioJjHY/OqFXcJKmsrUlLhzBEcFUfy4AaMm0PYAwyAzPAwDx7IXM0gXEfLvHCKp1AHFooss27k5AEcsOO9M4NnmxRuYcpbQv9God20bDoVA3P1kAMCPbzsT8BgCAnN9aM6Yqg3G3VTtaeb7Csvx/DQDcbAXELbZyqFbDm+mHP/z79L//yV9S3BUAIMCpk5E0KlACjIMARxW1L9YzdpW7fT5Or732KjNZCBjhoLPLZzhCYGrhhW5aBM5WokYrwyvRejtdPAQrALDOrGcmzQEAsAWwawAwi75HgDCenGTmGsreVvMFS5/BxMHBB6AFBwicC2SYyUQCI+lEotJzdp1y9080OdmR2Uhwpg8gyn+TxgmdRztz0b1Wh41+j8MSQB7GDfcDAJGAB7Rg4GgOBeRJm26cRlOAgWIAGvwTAzHEiw9k2WvAZRldzbD00fUTz41OyMNRYebieg8//JCOyc2bt3mvYI/AkcF41pqDBBrJ3oqAYL3ieHhODQACQGDzkwBF6TgS+FJW2Zok+XNmegQwhDUFcASMH4N/GJe1gZ34exqMouGHWJa4nthJakIDsNX3j/Vdl9XUTEasdfxd2Wt9BqDMeDKUw1GV4/BgDoaWg62caQu71wbOzsDjQHSmlc1p2oAzmKFslhMlUwAuWXocZda2VZyndZQcZNvW7UZpp9Hnq4Exfy+dkSPABR2yYDr7XPJzGQCsS6QZUABAj6CCpYmxP7z/t1uw/hQE4vXa6y8T2JJ2EPT+oGGF/1bXR+xHBJazywsGhlcXFwSmEGgJBO16DjUIyPvfikFjxjvXb8WqwXcajCzBdblmCwB6vhFw1eUrddCs/au/s+t6lMTi3hxgsXkN7n+kpjvYk2LswI6Apaj7ws6n3URmfr1O77//vpouhZB6L7SasF7wudOzKUuM0cVQmooKaBg8kakQ0g3BEsC6pN0BQxCBenR13yz1/qfPHvPzsFMG1GmvomkSz1gwkS3WHm5FCazLGteeiQDX7ydz9HB2l/vsQInQdQAgvyfsDbozGszrsA+jpK92XPfZiYXZU3yw4wCg3qP9eOj1POAv+5NtBt4amweK3hTcBqsupBjwve5SrcTKPouA69AJ8b1ujN2E8bHKjL2uuVUztfrZPRTlbHdiWDfg0tFcclsloFmuZ02luGjRDHw+8yDv5SMMQK3bOjhsKlXCviNg4+sIAFjKf8v9cP1RrkqJgOxbRmLaZwzvsQEAFTgXxmEWXY/mEt17jvXG/eemXfITsAbZaGnSxB+BA+RzJ36G/ZWWaWgdW5N0Ned6moxgtzf0D3kWra7YFfhr/+S3053b6HgqiQIAUgRUAgC0+50luvvqSD4dT2j3oJ0GBvJPf/xBevjoYer3TgO80n0AIJQWs+bqaifNy+GN2+nVV19Lo1PIsmCWVKmx3c2D4acH668DqKvWEddVH/aySipU2sC1pAVruAAAJdkUJFJV+huATUMQsNav9+VmpPknUzx077yeMAf2H92kw2vFJcHzq8t0eXXJ7sds6BDNn3D+4H5wrnG9hT1HAujsFFUQSpLCzzT4Ry24kH5xoqJmrWpPgBEI+qDGGwCObHlcJzfT051KhkJMbw14Xa0A3yIAaCekd9JU82u+uuS5hXMvLsB/ciUW2edqMFd/rpbNqe1qa+72AY+iAZh9IPulKJmOplyYd+jcrgeSyAGxgZIlBp5if6zDbx0ifqJWMYDqJ2k4OI2mNqrs+dSbr6ff/d3fTadT+bPZbuT4Xs1/rDHvRDmbmoSmYj5T6Q+qCRdeGBcw0Th/LEt2B2n8XQlUrQH54JgzxEGQE9mu51Fpo+RyDQAyAb+OEuDv/YDxivpwbAgAMgEdhntjICWSqdLARqVGzGpVNVTbsNZ+djRvkVgdTzj3uA+uN1dPxTzgewG8wv+G5in8H44D/GJ8fqszpgUBfQ+5MiEkDwzA5ffHAXaxUnd0MAChgY/vYvy3lDb+JBLMtTZf/Z1ea/k8OcLE29O0daKhOQe7Z0reTvk/jHvsDpzvLVhXf9pNXvO5fwAAlJ0I+1dp4Nb7yZ//JABA3Z+rk7p4mH3BwuXMI6N18K/+6I+5BPuBiQ6BUtdUxowcupTTiLAulJtg7o/xR/pNZgA2DkShnHa7j5kqioszONs2B/gRKuSxm9kGtbmlUHohZmpqW4ITBn0dGgVZw6Hyu3B/rkFvYUoz9uqSBh5UDSMxT9fz/TlmSg69WgAJjky9YOrF7g1ZO2VZkyc+VQDBeCKW2RXtAB909UHj5+KCa5qAHCsBLvepL84VrfGBlmHoZ29LkffbajczwZLTflqtZyq5S+oi+Ww7Tn/9199O/+kbf8MDa9M/ocN6NhZQN9ipK9QgNCbQHRKlZedTdYvcbQVCuSmHyxHB/gHwchldJgcou2JAWzJdBk/4TNW+EDVbCwEHEV7DCExN4/dGt+HGOua85rLW6LJG0sQ2bfrjnBmDA4VnwoF48/wWD3aDMLhnH8xc1+GQ9McA5dx8YpuWi4263oYT40PZXYBbTYtVMGjwLDjIeG2AV3DQoq29fgfwZMRSKGowogyZzUm24RwJ0MpMOwb3ALiqxhi0Gd2AZUMQTOW0LmWhI0MnAtkojTOygQAhAACydGS307pACSLnT3PD8ujIYOK7cPgyAxfzhRJm2S6VBA3J+ESGW4GBWZIuCTZDCI4UtRIJvg2onUKnGfcNIBRiy3QG9XwDzAsO4K2YmWaMsSPYasXxw3o+PS0dlbWHmgxwLsd284zIuIU2IJiS9cusDoJ4oQ9JJhdLstHlPMbW9izsaLEXkYGPMl+zAM2gy4dotsdF68nOkhmrdMQWaoaRnY2maRNLMJtyAwamLS5wREqiBQCLPQ3WXOUAYJ6g5WNGHTP+WLMIaN2dNw0zAIhr3b5zHpqZAjmhdUlwO7p0U7uSoC4c0RkZgGS8LVXqpE6Ctb6lACc6eACT19HkxOdpOIO1Bl09fjUIiP82AOg1cAgwrEsxuDajazI+j4BagEBo9OTYR+MEqQHse4L9DNgBoCoRQCF3ikxDgHxF4BMsGIB5uB73xVAaRbMrdRVEl0H8HplwgN+2o7A11PV0ExQDvwZAnchAN8TZLD19rG6ImE8BTQLW50vIA6x4jtNeReBZMwAZLLoL9p6djNO59iMqAPAQ4689948BgJz33OxAfl7ahYg+Oz5vcwBHECD2Pte0pUPiXgzo7u2TPckNd/S1/9itsEAG268WAJQha0pfD0ijsIKl+b0BcA8dQFQybpvA4rouwAjQD/lIB++VTM/iHyuICr+s1Xq2v31Nk5O8r6pJrucX81k/fwsAHrr3erzNADQjoS6n5WmwV8JcJXdgJ68BANtxKr6NzlD4g5w7VxYFKGgbaYkL+pg+P6JpkgBClfjVvlELAuJ8NlPYABxsAQDA4USVGnkdVwC9fBID9GJg43y3NivvH6w+JEpX+uDZFEnScepvV/QDvvKl++mtt95K09OQNCFQBJ8ZiTPID8R6ie8dDVWRsV6q6+q3/+/vKNEzuU17t1r3+fcJpFpQKsrERj9drlWWOb51M52cnqY+dYv7aRBdfom4wl80RwsJSHTpDMmH2obUACD8llzFRB2+LljgsySvUzejygFpgLxNmSnezzmN0tyyTgJoDv9LycByHqP7Ol444+BbXV08zpIsvF6s2azZh3YrkMzYSEsXSXr4SkjEsrFclMgZFGISumKo+/nctCo3NYrzEhA8z1czqhuKMs4Y+S4lSY73w04QoLTWJBP/8EXEmJOGJRJgsn/oHs29vNP66/cm9O/QFZpdpM1cjAMCa4x7DQQD6+xW3V0d77aSLbZX9ok4LkhIht2EHxonFM/L8VRVJaiEwWvFJmcb+un4fpeo9jbhh6wBSM3SoH/K9z+70rl75855+trXfj+dnOCMntL/JyAcAKDtUFknhelooK/EGthrumf8jSX42Mccb2lU41ymBA/a/2Bc19CDBICsOGc5X6QPPvwwrRkXjiLxh70LsBU2Z6sKHIDvy2X67vd+IE30pe5ruVIcMxidSGsyktm2s/jmdu/IjhX/Gj8z6dj8rt6r6DbM+Cj8IHd1tkb4KkmfeNcbprOz0zSdjrV+gjkKYJZAYNXP3Trk9mu0XiMZVQG8rP4Jv/VqicTFMPVGZ+n+vXvUSUY8hWYjsjtBHnCpfdjzPeZ+21S0YbO2AKC0qWtwJICwGKRa2udQ4rGVULH2oM/N7APFV/j3GcDLXTz1hblyNr6//U7cXX0mu+nRoRJeEhiuxbO68VodR3VseoPjZU3S/+kP/8MOE1kDgDTOuZtJ2fA2XDR4MSD/PwAYAXBT2uZJfhEA0Butnjj/99FMc7zhowCAfGslwl0vRP4pNtKLAoD4bBbbbAJ6G5HMjKkAQBquOEDb5/tVA4A4SOEgoFsTDNejZT9985vfTH/6TWg7XKb+9BbHZzKAiDQ0AFXqsJ1d8sB46c6N9Prrr6dxOHSrpbr/WozapTs4LJFpfPZMpSK5u+QAncUK1T078kcAQFHcdwQAecjZEIWofHY4wuDmwDGh1FPdVPEvMjec9/g8WH4KzMfUiDPjxsCUgvUtta8obj1W8w8E1Txoo4QZ9+ZMO+1FM8FqTKGSXjoKPJQFALI0ZzBU17UQ5fXzGsikzgap7Rt1D5vgoFUHZNwPnBZo47QadC0ASAAHgF5ouVmUXM7ENp2cghE4TejwCT3A87MbyhZutunx40c84P2iA06QR2W2LAmI+8xA3kZsRqwLOoBIFUZJYXZKwILMWTnZXzheZjUywEBmGWN1im7TAtfkUJRmM5rnnTTnyKQA+Kughk4bGiWEFk0BdroMB9sFOysqFY7gYTRMp2cnRasH38dSXLANxOasgyYyEKNkgSNAxpcYYC0A6G5mWBO0i8Fa6hxqfOaSLZdGDkAdANESyQYj1c8gNo1KJOhsV44556DSHDRQUJygcCxqMLFiUDvobAMZALQ1+A8tHYPjBEYDTEZpPucHIVolJn5+Y6IudREQgO2oQEABNBxSiYNHSdV8ka5mV+nq4hlLUv1c3mMGtNm1Gw72qnTq9Bwr2BYgZIZSCwz4ebcsxy/Zx2Pvs52njlWUBMvOFGaDSuHFAMXzEcBGMhKZ5bGa3JBByW7WoYFJMobK2bHmsE9V/i47cnZ2i+P08/fhzCNYV8D8yqt3VWo8ULmwAzu814xjBH1oskLAH7aGzaIE6uv3A2qSeo1g7V8tAvAHAzDWOucgACA3P3gRAFD2swbQnl/18HEAQO+rknj95ABA7r/Y4CXBGUzH5zSP0Lh1JT9qP8X+kgEwz4PtiX6OAN2lcSH5kPf7c4B9jcnhhEh7HyUw0P2acXYdAAj/uWZa1/atLvetYdBDAGD9OT7bgRLg9p75dGZkV5UGNeB/DAAEIKHR2WcA+vmPjZF+78SEk5pFy5X2pwEeDUjXpdQCfKIxVVVy3A0I1WSj4DIBKAUAOEDmtkowu/mH14cbP8FusiQ3GjixJBJAx3zOz68XKl2cRCMuJIJxDn7pt15jaePNWwJIYOHVTAtMRGjArpRAjfN5Oj2lvfqHH7+b/u7v/i4u4z8AACAASURBVC5tN0p0DEc39f0jaK7BXwmgqD+g9vJuosTjbgrW4ApKfgQmN+GPZk2+AJII/IWGreajq6XIBu3U3AumYFRpmIHl9e5kaDk7lDgtiWgl2Otu9PgMdZNHo7TMCRA3ftNKli9UQB5o7eI646GYfU8ePyEjEpIMSPQxKYrnCY1ZXoOfl5+Fc1Z+os7T9XohbemtmGH2a+0XHNortDPhY+j8U3MTv3jGHmAiu7KDQEswwAAA4v3rhUW8DXxKN5kMTOqU6v4MYAMA5L1uo9N9P9bhOvSSo4syfT8kmI91KW/sXmGBde0droHzzj6M4wYm4AZDJpoJ/AzU9GFdKG2R8NXobFc6D1eLLeOKbQCCs5CgOD+fpK997WsEACfjCUvI6ddAo3zvGTQimuPi/3lN1n6dSQWII8iUawDAFZpUwKfbiomIPQkAHec7miEulpdqrod1Ro1LMy1VqQQAEH7Fd77z/QwAYhwABHL/oUkkSrQt0RTrR81AzPYua6gkXks8Xtuz+hnhj8wX0rke9ZW8RKk9E7uRrOiNB7Q3vSAmIGHPRDQqrNBEZjeKJpV5GcuuRyK0BcDIwK5KjOFAYR8ZANz1p+nV115NL999mXutBQCVsCpJhJYoZLvRJhbzc9cdieEL74GjMZbHJEGa6oMWADQgWfvgtA8HAEAmcBqiQAsAdkdVQGrnfDJF9AjQdx3u0/onLwwA/o9/+O93NExh0MBIwmsQWgZiEGGhxsBWGoByRMKBz7XIzSM3Xd9KCYgzX6Ed4NrpDkMD/NTG2W0MV2+rg7W8ymayEfdkhinqTMAqAAh/3kg4vhWfg0oHXzmz271+yzBzpt+agHZAPTGZAuuF0yzIXLsfC67NbLcLyhn8Ywul++ylOYa1Y45+zho2RdaSX92WBDuz2I7/QbS9IxYd8+ruOs4c7lXkdMc7O5zxhUcz0HsDVdZRXco0hoYIDOEYbI01OiXwkP6Hn1+kP/3TP0t/88N3+Xs4XjzoonsbMi7UuRsIYDmb9CliO+6tyUCx9kkZ3+46Xs3nLNmDyeZBEiwGARRwAOL9TYCU10PWcowHpZ4VxKiD4RPNRMxQ8l6w8cGhhfu/XK54MA/HovCDAelmFnCsJhR5RemhMu1gzFp7j00gkjJKDqJZDh1aK9a50sLpdhDltXgoRgnIQCwxHj549p4cZaxf3CcCBgCzyORR72S55t/74x0ZQQIF0CkPDol0L8DUASOO+5jBuIAFlzZT524rRp6DsOUCGjJg9gzT6SkyZtOs7wfHDIAa/k5nAddlKUM4ySgFwgGLDDPvEx0AF+Gwiekl8X1lJFlya+0Kd2Ojjg7qo1ymoJIElMDguXtDNd9AJp6O7RRMJh3CfMEpY8mPS2D0fgYHcBxZ7qi5J6Oz2W9tMOrDGgzV+uXfn51Mo9uxmKMz6GxlcE1NL3A/yLzjtdysclMXAl5NxtxMP9yXQCsBiSl3S203tstlS+aUn4NjxnIVaVvKKVQHSM+31x8dmiixld1X4FCvi49cuohzsM64Ryms7WEHsEQgNJzk95PBSdFsMPxGZKj5xWQDS6zAflWwI0ArGMShTQTbw1Lgq8vMjODzB5BO+Xk04Ygg2/apJKqcQe0CL7IjpelHDuCjeUy9bmo9rpoxwaRGc95NowticQoFbBb9R49BlKyA8crgUWt+cqK1TWF9ajmhVE8dlLkdUo8BzPvvPeD+GUWXcgcVd1+9y0ZFZzfRwAhrReXDKB1jad1Cex37D8xm2uvBMOuegmkpo6XADaArvg+ALuxR67jq2SSboED5+dq9BwPKuhohhqdu2kFzG8zlvH7yflJAnF+tRqb9nHabxc+ZyRg/g8mkl9bUi7w457kEoJS8ct/lElxdcxvai7jzjm/h0v4IrGt7hvWGLpnc+8Hk4vhXjYBsZ9r7bkuxW1DJ78/7IANoXX/FXR/ZNbXSAsz77hoJG5tnlpEfaATTlvZYS0y2q2gU1kwI/s1+1zWBEpjGnl8/c+1TtpqM7ThmUDYeuFQ66J29pADUiVKtXWllHXoxwK3WGRJI/rn9V2PQALjRXML2pjedigFjZooHnIBBd3N01l34ZRbxV+IQfptK/6DtiP0wHuzIAPzSO2+kN998I/XTE4Jby9mjaNqRmNwY7MBUHqTZZS997/vfSz/7h0f0aXpDJSDBVKRswmREG4MKANi901u3o8toAN092UdXNhloyvFHPGf+GaBZNSZONDkQzk2kKo3Yeh1oD2v+4C+COCJgtjCcaGsBJiJxGvNnX2/Y1xnPCpHsa+q84isqFgA4YRx26xVLMy+fPaEdnYzUjI7/R3lnlFYauJyO0V0e/mGAfOtVSK0IWBtsnUhTJQCANibUo+TZAbYBaLzf5cr4vIHwsla13mpGbdYZ9jjBN1yDsb5Ko9FpZsAqySnmWk5mbuI8zEBXJIQ3SoBDwkd+gSs0xCxFOOWE8aF9lAkCCYlg+BYCtnpRaWNJEOg8k225Qgk7gCzFPXM0mCNj3mMf44t5ZDUN1ivO52kk5EKbLk05b9sN9Jm3aY2JSSndOjtNX/jCF9L9V15i/DQFEAgAKTTMZbPRZCGapEVVgxnadUxnANB+hJvs0a/eKDktHcBtWqyXqgTYKLEHoFnNv5bSAIzmjohzpY8NP1w+pDQoUaL/NP3kJ+9SEgiAn7o4A8Rep10f1Tdel7u8LlqAiTImlu8JiR37NN5jrvKqE667XAHphIz8XifOucYxH9NJOj05SdPT0zSejKmdhw7lkwAMS6m7E91dO3vMHm9R0TIAqxNrFrHsKZsMvX7/vgg1KyVI1d28n9DGkM9D4kUvaxHuM+BKwp1nwt45ZW3WLqC2q/Ch+pyqtUdpn3LcU/xLrhdKTFRn+AGJlXqtYT3SX8/XK5+tzx6PX8voQ1MnvxTzq8Hh3utAZQXe4wRYvkbY3Wyjq4ljvNW3nxbj+0kBgOjSdfB1AADk+3qRKd1+cgCgHID9wfvHBgDKUdRo1iW8h8YXW4rvO+IwfZIAoK7VltC2EUR345aFmBHP+FVkvPLKjc/9igFAlGqgRMAlrdtgkn33Rw/Sn/3Z19NPPlCXquHoPDKM6mqEYmGWgCHzMhymu7dO0+uvv8bSYIo9GwQJx8caXzZm66W6lGG5dkt4uhNZ24JOqVNVMqPtJFZWDQDq98VBznMBgCT00hbRJazPA7ufVvkLxfAb93QQo2SCjLuBMn/DCRwrhNfopuluaMgqCSgEHuWSOgGKYkJhDdUZIJf4QJPLIB2DHY6vKPm6fmSpc/Aqptd8ealuZNQJU/MOHCxgAkGc11pDZOYRKBvz7xDPpvh2EpAoIe4BAwHfI5xfdNb1ixnZndhGGAs6acPIyEdpMrLmdMCjlBqAjQLjSHhEaaup5tZUwW5gt7vtSv8GmKHOpHKomRkObTM4xDwAxipxZMkUgVOBH0xdBCOQgcIpAOwBS4Zx79DNIUPpQJcMzY/2p4ESA4AO6GybdtHli+s6Shu73Q313OfnZ2JMYTyC9Ybvt2ahx7gFAPF7avsEkMpyazyfG1nkNthdu+SOhGBKuLEJA2KzCtCZDaC2mdshjZC1QVhSUV/zwKEcgXQHHKgAQO6DYGcakLLj7T1wgtqweOHeVuvQhumpU6ntt0uo8XmVx0HDDuwRlZoASMQLzw2giut/PuceIDtuEfvSAWBoLiGAqUt0HbhRY6/6ficSVAqC7xfTdIMOkJW+Yjbn1khqurQWEX/tM5Qgu+QKen94Do8VrgVJAa4tM32iMYi6KPbT9EwAIPa+nFg5pFqPYpjDsf/5g/cJ6A17Au8cNAK/wufR1IiAPwHFMQF26irBNoBhQpL0Rk1TeuoeyCTAWoxePJdLz2kncUbE3HOvVBnkXwQA3HMOXxAAFKge7NcoQcsLUAdGjHfnt/mHTxIA5NeF51wDmG3JOOfSXR/jTjIYcwQAzNpWPl/jcy4FdlKgMFD1BgGEEUw1ukyHRuRFAEB+PpigBggAKNSvNnG6r1DYvYtDACC/JlgWeb81AdRHBQAzQB3rogVCrYl0eLVEp1MCG3FeZamTSLCjBI22olzhGAAIu+OSyzoRynVU6VcfAmttyw12+TOwX5T/sBSQmxK5u2voBbbPlytutsFmsZQEJEkMAMIOrRZMLPz+73w+ffnLX04nk5k0SndXsn+DMSsL+ptpeu+9d9P3v/ez9OC9B6nfO6dW6eTsZjo/O88J6sGJEnkAVpCI2IZ9ReKW5z0SgPidNeOi6URmYDvhiFxMgH94NgMAWapsLzFYZCP2g/HShIGeBzWtSzdWnldhk9mUrGIAAQDUmS/AymAHSleRaM2aiz0lRJ89fcyuq7g+mFkovS5ADxhBoV0ZwPAUIA2+uxfN1kJzeRfdT0YJXVKLD5grleJ8yc2uY2AEGFZBfjQhsp+kCgtoMkaTN1c8xGdcyaJqGZxt0rSF/ystZkv8ROlqNLksIKImhky0aPLWORPc9DD2Wwbg68ZbYecJeK6ks6sEDtBr+KHqcov7wn8TIKf+Ngo5t2k2m7N7LxNju5WqM9jUBwBKAHSxz5EQpmTHQOs2JVUYQQsQfv3lZkfA6O7NG+n+G2+kV+7c1Hl8GtIAwxIf8XOxL1W5I43Q2mbbhmftOsZFkn44BABCQsQsX+pcR/yy267JVESTL44hNbAFADreYTO33Yrdgt9990F69uyCpb+YWydOVhEew94TqI0F1cblnkP7JbY3OVHRJDLy+7dYv2rSgj0MRqzPPuwzyaFsUm80pPYl/Bxo3KP5xScBAK7JFO6nxVL2ZDg+43y+du+emrBFF+Ze3xIEOpuyHc/nUutfPx8AbIlItvkGAPdwjwbwy0QsM9GDgPQ8ALBthMZz1hqOBwBAnTGREAi72+I0NQDIOT9SkbAvZaYV0gKARP7rMuNWWi9TFmN8/9Uf/jveOgJfOs/xIBCHPHRD2ZG1Q7CnwXf4cwWYayc6NLrCUfPChyHPHekOeReZkdfVoDr01g4SnD0N3YcmvBZ9DkelrZlugUVPVC79DTqvHZk88DJQhxiARGQDEc/P7cxsHGRtCeOeIxIL+FgX4uMAoK5kzZpD46b5twaOHdUXBwC7DkPz+aNffDjg3mMAXuchN9cvmzgYImRuoIURAsFxWvUlVvtX3/5++s//+c/Tg0cLdT0d39Bw7FD6izBwSSAF3dRg0N6+9woNH0oKyBi0+HDuIieQBvOhg0gZY2SZZbxK0JrBDR705QERUO4xkbLDGlpmLhG0rlkl3lwfLu4iDECLByg7Xe3YnRgvlE7i+aajMTPV0Loyg6fOJAz60/SsKjdEiSNLTnGNlXRwoJVhBpNLhH2w8XBmSaAcNWvl4fME2WK/KmgFMKbMul/UAGNjjIGYUQAeA9wEsOeS1dFIDQQoVoxM++xK8zcYEmBCCA9HBSUm3BcRJFsXEb8j824EMHRNxwOgQ38ohuB2u0qXl1cEZfA8AEzhQDA7RJ0dzSMy2/xdaGoYANsiI8wShejQGho4Yj9KI8ljhrHMOmqRQeXvUEpkrUc4lmTYgSE5TCcn57x/aLaogcmF7GssL68Na7hhPvB9YjHBWe1uJIMI6wU6ISsrjvODAFF0dNY6EvAEpwPj6wwYwRk4mNX356C9Lgnuhcj1Qt2T914BAPqgtcPuPQQRdYo8113euE6iNGMVGbEKULdoN8e7clA6jr9LD5qSYB/UNQuQgVZWeS/MCPxuEgy4zFzICRaNp7/TQuA1gEgN0oEYHzV4zr2zWnL9QW8HTNir2UwZ6WgwgtIg7kkDn2ZcVUxsBQ9l4uvy3n5S999daOC5i3lmLFVd7HAd71kDvN7n1v6D/aOI9FialC3wDEZHXqNVuSKAcGo7Ibs9RYkX9IiKLh8+g3F4/PBhBCwAlJe0IzLoAfYYSBv2yAi889JLvOYqxMMBlNNORXdv/AyQFSXAmAesMycseGab8eUmNuFnaF1uWRLEpjpHMrte5y2DvN0Dx0h3ZgB6DdkPyGMIO/TrBAAbQKllMNbMddrNJsG5C0e2BntathenN5ZvLmlip3OVDcIO4lzzi0yN2O/utljbm/q7HIDkwOM5DEBeI9bDHoDqdViBWDx/4nzYN3jd3+wDgPE8FkevdBwP6SEdAnMOMd0yUNssuHpevMYgU7D/ioRS+KsGJhAo61yr5uEAA9DzYabeoXEpwfJxJmoNBPJ8Qg3DGBqjQWAwCycSIARZDiS+DQDCPLIsuB9MKATiAHIQGCNRhWTb5WX6va98Lv32V7+aXrl7QrsxGUKTdJBunt2jlvD3vv+T9JOf/CTtdmhQ0UvLFcp+h6l/Mkm3b91O47MJk3bDk1E6OTmlThoTJ6itVKGr/kUzM+iqxrpmh14mBxXfuWQVzSvMMNe4oalSYdG3jBYDc4fGnWtopyYp1tS1nXepLatnrC/LBGWYX5T1jiEjsdMZtVTXdQAE2KPT8ZgJ+tnlMzKtlsEoMoMQwGbN6AXWh3GVnvNAiUPeH/wHPGM03UMpKH2CYNYv9XfLu7CYko1atC6oXUzfPZKPXmIV0KbzIbR/my6eHBdWIDgJrhEAOxF+KrrRyifSuMD/VmXJfrwk4Da0KaPJFTWk40ypYz6Mk5q7uUlEIQTgfWiiRwB0HZrA/SHHG82hsI5G0d0X98Tr7BTfzOdX9DEWfeiE4/oxoX2c3cHaRkMNP5AlS3aKM0AcADB+9/W76datW+nmiaptTieqWDk7m8hnCiC3D4CUlTDS2oV2IrtYV/qLXRZgaCBaizpY92YASm4E8y0CiK6jKhb63AFgL4PhD//cwKgb0yDeAFMQr4vQdF+u5W+yezhLZYNhGnGWtRG9j+hbB3nDQLS0CLsM5Drm8XrXPEu+KGu6YcioESkZBbwHcQ5esBvnN28wnoPdRbwBdcNuxUqx3ZRGOtKwy/e/7KGb+CDNFuoqPhxDJ/1muvf6G5yv7RaEmcL0N03MzGlXnB5jAB46R1pMA+95UQAQhBm85DeXZz6muXeMYHUdANje/951QoP5kF39KL8DEUmvSNzmCsCwG24uZHtVEfI4LzUAiMu4XftvEgB4CHlVhgJdh34xANAZn4IIFwCQhip7pk1ph5tpHAAAuRg/IgC459hWACCntVqchxaEmTm/LACw1Mi/GAB4fPH++gDA7jqyQypmB4AkBHyXK3WE/Ppf/df0rW99Kz2d9aLETEwwA4D9HbSeVsy4IAD//NtvqCnFRuL7zihT2ysyVexGFQAPmHp2gnUIRAlHlDPogMKB68y5neQGBMlaPLYDBUBisJdLGcqM1AE5Sg7x/KsoEQMjSPfVU4kB2UUjdstjc4uB2C9LiE6jbLc/SY8fPyb4hXE4P9dBjkMSmdrTMzC/ijYbGXkNmlQHAOjKaUDPLA04sfgMtSwqIIoltVWXUzLQQPEHkBaNJ5glJ7tPdgKlCQDKkF0mYzLGbziO5gK9okeI94M95BdZhGN10RoMRlwv0GyDowmghE0YFmgCAAdKALHAvy4DkOsoSyrgkESXMmnnZe25oL3j+3VASXMELzqK0bgkUYNN9PsxyjHDiUfpATXUoowJGokEYwGQ9PppFp2JHWgTBCbrMMSnwRbsDygcjGsYqKkPS94HEhgV2FNrZXB8o+uhAUAndli+jLlhU5DI4jeleXpWiVjDwQWgjDF192KViitDXYCOEvzh96vVlnPt0l/Z9NJ1Go42naWoxGRmvupGWTRBVDLiVx1I1sDAIQDQ5UMyIPU1elk8XswFNHURWG5mrMZAY8USVYiXB6iLOW/BNYHg6jwoJqAYgMhSm7GGOUbgJEZmlEIlJQJy8ysDgghs607xLm8OABDtzvX8AVhWht9lLfiXGk0BHsiB1by6VIZtyMiEkHi9g8XhYJTnw6C87QeABpQA4nMEAUPkWmzfSCiwtGeWnj15SiB0A7CfALNKiB14YcgJ5IV4/8t377KTHZgWCGLmlzOO/xAM4tAuRBIBQL+Yf8GQDsaQHWcHGk40ap/hvBFjuwUAWUZYgSEfFwBsS0l+UwHAvJ8azChrCvtYqzAdJjaQEGnB9wNOB7pcc9u5lGevo3J0f4xzuQX2+NkDKOueFEr+7u75jPnjeRr/tn5aO//+Ls/7dWXV+939XKof3V0rAJC2xAnDAyXALftQ79fAC0CK/667RrIDd4BfZLmERmmMh9dz1kJkqWdJdr4oAHjoeQsDuWhLHZqzenl4j63Q+Rv2YhSAT1RMGGh02Z39MCfIagagEnK6OtcltV31M5puwO975dY0feYzn05f/uJbShguH9NPunzWT9/73nfTu+894fv7/Rvp7Pw89QfnKikc9dPt27fT+Z2btGMAAJHogK8lDa8IALO2nBg2YOZgXjIzOgBAn/dIABXAtO6uqvuuAcA870fAAIyJCCQ9+pv0F6I5oBIykgGp14/9BFeJ4F4vr67SlknbIf1ODeiOycqnjx/SfluzFX+C/41mEdmGRDd7JBZhq+n/BEADpoyAiGDIrHGOglUow5PxPJQco0x4FX5AANaQYuDe6UvrDow4+L+jvpLdZF9FIzn9K4ALPqK+IAJ0d70Pv/zqUkm6K3Qxhk/KUnI1xSLQZIYbYpTw5TiOPb3PDDNLdHhOcWbxPgcotRVzDedule/gbV1cXfB8QxzNyqb+UAzxrfSsN9Cyi/2q81f+gpthXG1V8rpahqTFoEiXwBYgAUygcYTqnElaRwn1vXtvpc98+tNp2Vuq5NmJl+2SieHzG1M2U8RCJNA9UhNAPKcBQIyxbQ/uyWccm1lVCUOewdDhC2km61MbADQJAe+DfjJ8borogNm2mGkZVgCgk99sGBYJUmm6r1nRYgYgxnPFRFsNBEbTv1i09ltNWsC/eL7lUmW8WItqDij/k88bnYwPAYAtkIa1yO7jAbSf3jgno3g4GqcV9k9oTxs0LNI3sSMi5sibrPkPMwBbAPD+/Tdj7Sl+8vkBwE9EM+3b3JRor3LzMAHoGPPPt1WXANdrwt3HDagXvOf5iSdf9x8TAKgF65KTILTklIv8pjzPf/Sv/2dawENlPPVctwOQxaLbZJ8NXRYGb5dOM7E7GdDW8YW4YxewObwgtlmjpHzPXhlF4/SYGSO77AcIRJhaR2LcdAHAvBRkAGNAs4SNBT1DoyCLUzqQcgmEKaPx+RYA9DgfW3DtaO6JWF7TVa4V2dz/nnacu4CdSwtzoJtv6DCF95jhaDdift8xCmy8YY8/eogVdOBLWxDZh4UBLwRkOKAuZst0cXmZ/uQb/xcds11fzKlemkbXNpUw9lbqtjkabnnIf/bt+2LErOc8QEcWfY8S2sV6S0CMa65i2lrnQWBfsMQqSn27L/YyJUcAQB+MLjXV96rrmpiEeknbY53gB+HvKPGFOD6agPBQQgcpOPUJQsvDNJqM6IgAMEV2EY4Dm22gs+wIzqm0+HBNOGz4nfe3guHipBcwRmAE91UFADrQR5YUTgwBVzeRASMHJcsbOSR4qfSvaFzZcfH3qDxZGWe+H9+5VuYbTQVYXorPR5CO90hfrIwXzIUcJHcl7nZ1YjaXTTCi2UeUBpnJyxJLXMTdNJElC2AY4w17QmCTrKI1xYit2WVtRGu7AMDYRoYVQAkcpKwFSHHeIUVyAX75OeioosEKM/aDtFnKIZb+TCnRJqNyKGBFE6iSa++bzNAhuNpjptDOJx2rCAoxdFgvKHPCujKIhv2mDG50H60ctnr74lmlkdkjsE6AlU5llBQgdmkAQAdpDEjYXU/NYfBi58QAEqnhCaZAFZwYZCrPEplkN+bwWVIx+upgE99XB0xmRpZn6mp8bCMAgV1TwxSVqpsp5AylGQ/QlMQLQtUE5IMpCyBNDn+x1yzpD03O2eySTBQDgXDg8f42IMgZzaxNWQJ8fFcJGkPEGl3R626n9eSxdFcvdvYOAX2uATMut9KG4r1XwCcAAzIUG+Yp7JDtCf9FN02ybGW3JlOwedQshOA/ynihuQV9yqurtJzNaafJ0sHiJKC8oj1TEDdgufytl26m05NT3gPs2IP3H3Dtjftjsllv377DINyAv8F/llS7BDm0f3S/ADvZczACvNBirPynQwzXXdMM4aMyAEtGuJyYDCyDbZBt5nM0AN0cqbMfW021j6sBWFVQ1EnObFfi74eAwBoUM9Davs/3XCo7yziYTSYAWt2QYXt5zpFNDvZRVIY0418zpmk3m/VeVyjwDK6aU+jQ0VlS3U2+gu3GdaAf92F8qg14tEcLk8vXrBM3uoFoMhAO6CHwj+u2AgDr/e//zozzWFfyO8rTKRiGvm+tFa7uyATiK/+9Zjy3lSm5E2azX0qCbV80/zoQkMNASQEkV2B/AgSMhJ0SBS4LjwRtdAWnlnBOfBSGC9YO/UCeZ9uscdVbLdKrr76avvbVz6fPv/NOWm3UoOm/fv3/oa90tVTTosH0LhM3KI2Enblcz9L5+Y30yr3bXJ8gZIHdtktIdKKSRMw+a022APLkVAkL+IGUJGDCpFR6tWL/PDP53FEiVoHfdeDoRYsu0vpOvd8+gm3pCcqbI55S4seMFV0B9t2SKrA3U7CX0Mgr/ChrMpPFb5+pYpQ7nnHSyOPu+xmFLfb6tjSPE3vbSAwDSHTCSmBMMMRif2yYWOsnNL0CMxOJYIzneHpbCaERGpuMCEjWe20yiqZe0a26MFh1fXi0akAnf9GajTUACJ9lfrkgQLqag3W+SuvlFc+t4QCaepCO6VoiesKRICMgG9rHaK5hv1gHqfz11VbdeZdppBLc2y+pNPe11+lDPJ0/IyCNphj0DUMC6OJDdWGeXc0JsO22kZQM4BLxAIBqJOfu37uf7t5+NZYO9t0gbdIFq3HAlMULjDTqb5+N+f3ossB13oeeNBiJYmxiX9bPUY+59BCjhDpKgCWpA7ZDaVBHPzUYpZhD3Cf8IwKMwegFw0+yEYUB2AEACmCDNgAAEsZJREFU4VNuNtRdpv/ek/xPry/9X+MEuddD1UOBwx/MQN+/Ew7YTrjOxcUl/XInvvn+yg/fBCDr/Wh2IJiNvJ9gWPvv6B5+586dNB5P0xKNzAK4Z1l+DQxFkucQqz7vfdi5iLuuFtKthKYlNQDfuE8txdXqquNnutnsPgCYnyCfh2Eh8nrZO8Oa5BxtV+A2vog/cwwAPCQZp3OhuY34sWXwFwZgd/+1H29hMieEByHhlr/tWEXInpSePlHwnLCrOdFgFm6JXTkWvo6lV1oAUAf7Pkvr1wUAuhuhMtqRrYlDCX9bH7jXeups1PlcIXbLCW4COi+E2gE9BgDKUOtbftMBwHbTlO7Ouv99iu9xAJDGyeNtrYk82J8cAOiOh5qz7lp8UQDwEHu0c1ggKwaHqq9SkIdPLtOjR4/Sf/zzv0w//vGPU294S4DRVgZ9OIiS3SUaTGzSybRHg/rGa3dk8Fczad/RoQC4Ier11VyisliDyFxlw1HpmRkE5PqKACQb26zH1YiEOnOWceySgef6DQeWQTIO/yMAYBabR5ksAmhQ+yFqf3FJw25m3QTlwNC4gzwvwJnRlO9zAAZgIZtyODaRuQQoRuYRmhVQp0bzKpaYgnc7zv48HGMcIjicUB6CzBxE/Ll/AbIiqBj21HmWpbc4gIqWoEFQ7lNm80TNh8NDp22IeY0S6K0+jxJgO8UA5twEBNegnsZOmWc4iHiNpyc5MwtAAN+Jw9qlA3QBqoxddgCxl3CQB/Mr26cAAAH8UUsM3xldh+vEBN4vAFCGCMwkswLpGLHsFNo7ovqDNWCxYwK3AEEOAIBlvRXWpoEpBwe13bM9wXO49JJ2JXe7EpBJ9iHATpa5oL29Sgacf6kztvy8S9jr7pRh95FxxviyhGFg4FHlsnWGkIC2gTF0UCQghuyqnDn8N8e9KdepzxnPX1nUBaTXh0ugxM/ZLoZDVTfB0DUaR6Gi6HN/RjMPOxq+NzZ2YQkRuhcO040b55xTPAc1/gBkh9NmQK3THXgNJ32WLi8vCATm0tY1uttWMgMBLWQR97AfnJNKvzMHmoMInCtHs7avPl+wh12OplGAswywRSLZXv9mPIhRWDPp3big20WOsU/FKMT+F/svEh4B2MMew37Mr2bcBwJLh2nMMV1RDNzPB73Km7dvqpv4ViLfP//5zznOcFwx7i/duUN2M8YY65vBE+xCgOh5DSFgqey5S0zynu6M736S80UBwJLcdElIF4xRCZrXbD8FISYvb6xn+lxOIDeB5SemAVgBwOXLK+ZRSB4UJpLeVa+nzs9OtDZsvewgH/JpkYQYyj7msyIAO/VhKuyXOmHZCdYaVpQBwPyeBgBEApXAY37oAtbRrh6r6a4H6RoA0DbtuQBYlgjpMqiarzkIAOr6wQYM/8XAcgtQk+kN0CS61rPbLUAyAB9RwnboPo9J07TroQYAO/dVsRXbZ+r8zJJFAP9KaDjZRMZU5de0zaps5+HP1OcAbUsAgPBf7ty4qbLPxRUBj9tnw/TOO++kXh+SIRdpcDnlOdRHd98hummqomA7mZKhs+zr7Lz/1qsCXoay8wCM+NrIf8F5it9nfT02EhikwVjzi9Qmgb/KLsrmdtdfDQDiuh2wKJg83f1qZp8AFyaVRpGEQXnrWgkrx2+40/qalxeqmoCNJBMTPicqKSCvwEYTaorBpI3PwCjZ5PMEU8ssQwI/VaDbAplqaqnu89RqnJ6JYRYJcAOF0KxmF+QYty060oJZGU3W4Ffh581W2nYYV57flT2HPwSJFCYVt9KdlUZimYf5RlIo0PJlaS8174okkLSqt6wU4HrsaU4gNQTgb7mUNiL96/AV2SSPPgF89Djfg4mG9VGzMfs9yPsM0iLW0fTmS+nVV19LN+6+QtDu6WyuRFmso10AmQDm6MuFxAruE5pywBn5nG6SE8C6E9O7jZp1IQGA772af8j5H7B8GU0H1R0aTE2sh5t3bqpCpidbLX9PALb9HfsmXpeMn3bhNx8BAN0EBAxR21wCgLNZsFelhQjmXw0AYmpYas0KFvnY8EfhIygmGasB4AYSSGs279E5ZSAmzuWQBKmP1zoxZea6GpOpaQybIrJJXAUnNRUwBpYcrz19esH3417ov/ThO6IM+IzXrQFA27Fs22um9wEjysRDD8ndQQIASPsznBIAvP+pNxnTrTcz/R7rHvgL/seERNilPQLPYRyhZf7V0ji+td90ALD2s038+mUBgD6fdrtIOFaSYByvAgBKA7B2PBR0HGbc5XWQkeymS9gRpLJcrwEXowa6MJ2iPKDSADSIU2sz6Z77pLp3Xg2C+tEBQF2FJW1BZe9eWOPhDE35WwtQhf7QAaCMVGczsbL2lTXg4pgMx/ujMgCz0au0o+r73gcA9dfsSDWMu3bW6/GLTzYD3k6Ar3DN+omrDGMcPurz7gOArWXqvuMY88+fAo7DAxEaE/1+enI5Tz979930f3z9L9ODBw9SGkmUtrdVZmfC0optSktlNk7OBune6/fSjfMQ4V8osyvHZJA20ZXtcrZQ198oD8D4w3A68BBTKbJUAYDUc2fHzMwn75edU0vuAtbo79QgmA27AWxfkww0i5VS1BUHm5hpmwWyjNgV0el2oowhNO9w0AEA5L4JLYXFcsaDHJlGvNzEg4c4AMMBggE53XYwVa7b49gUO6BSDHw3M5BPL3iATYJJgACN3ZdvnvE6BEFWKOuArpOykGbO4e9wQNg1uK+Sbrz4npW0ZhQEbtMYeo9DOCAqBZadwXtCQwed6qgfJdHd8Qm+P95H71olp3AmMD6jsbRAMH4q+Qu2s9d9AKHMaMJpj1JMaOGACeAGJdQOZAmKS7JUkuJmCC4dzR2B7XCghHKzIWPMJcEeH81/VXoe5SneG3huNrShvqJKXiwRYbB66GRM1rgKK8Gy7wFFpTEvCAq4FrcoB4mseBXQ54DZLNhqHduhk7agMsDKiK4yg62AeMVBUjYVcyeQmaVRVRdglYwXBqTe37Ufrf00UMmzIEqFuUbY7EeaLF4zXmM5qKiYEh7jYZx/zsDuQjw7M8iD3UjQlID6SuWup2cE6g1m5uYmuRRGY4UALJcPkwmngAtzgmd7+Oih9oLth4GUSvvPARKfea8UU+A6uo86sKjHzA4sgy4wVC1OH+fOdKL9U5qzaN9mH/dARjSXCGMvuERrG418BgD2oU8YpdNkeUFLapIBQLMp+dxgHBOIjPJ6iPiPZaNcOoQsvO0SAhsmUxI6W5+k26+8HOxkBWTQ9mPzEEAcLp2ytlTFjsp2P5gmuJd9dl8B4opGbHd91j2E5QDHykKX+Go9M4jk/8saZyB1gAH4qwQApUGL5lXFL6kT0IMMiDWMpAZ4y0FLLl3PRX06nwJ4877zXgdzlOsa3T/ZpTQAlaY0wwFLDvBDu2+bRbXLuGs/RaLgCADo+3BJsO+/BQCPEBH88cywbcXDc+IgcwW792edvl3VvK9mAZayf5dQd5Pv+QaiBFhC+od9Ptgra5TmCoBgCGWgP1/Qfnb+Rec/2tKtpsdQBib9oZbZ2IKNw7662FNNAgkzJACjG6PZ4JzP1g75552AIpeW+jo8H+E/REng6WhCf+/2dJJeeeXlNBnK7zgZSLv0cq7GZdvJDSUrewMyAJ8tntEHeuvTr6n7eWjYbTZoTgBbpOYZsJ8G/wiwsuGZNJEJOFEHkLCrxihKlUuXW5d3a569zvN4BSDfzsqIUhL0dpVAQeO06ZR+D14oodR9KjEMSI1MxIgvH7z3c94PuvXyvJpdpcePHqXVDMCZNOScmLGWLpN34YMNN9AgLpp/8I90HgV4uVOFgUA3+C9iwKHZlIDKG1nbmnp51oAeS7YCTfHURCQCaSdGwj6slv3Q1g3wM2w+mGqYrw/e/4D7YhCsLUj/CIgNIG5yKsA4FjYYTEykYu7IgI8SPvjPjB+d2JEPCr/P86UzRIw5ashhjJZRnk9gTpUrsod6nn5/ooqD8ZDzNr0tv2KOpiubbTq9oS7DBuTtn7qkczNQieduDe3AVdquBvKxrG0G8l4w+3FfOD+pjRnMf4jo0I/DPKHJFpuvQUN7zs+BQaiFA999kMYESqEQGXMacUQd29QAoJO8LQMQ4BeZgmjuQYAW8YYqbnQ2o9kHfIootV5J61eSNmoEgvN9Oj2lX/HBBx8EwBbzEfPiph+ZKZ3XT8RTkYAoYKbihT0GWZzb7u6se9ySAFEnmSVB0KPECEuary7pK0uiZJV26x2rccAEJOAY0g71Pvd6qgFWTsHB5JSYr/OlvhcU5bPzs3TvrTdp15arK9olanMiObATcG6AOCusHcGNWv9baxwoqM+lF+sCbL8ynyOhoZeP+7ZpRkVA6p5vxj0i9msofpmhf1APt/jRfUvYVZJxh0++OFtz00P9vKeh2PSmoEZkJNp45jfj3Pujfy0AkJOes2aHAcA6c5xLeLID4Rs8BgD59796ALAeUD/jPgNQ78qSYrmUuVsi/FEAQA1muOYVU+4YAFjfX9YOus7za1bJvlaf3vCLAoD7i7EF/H4xALDvrpzxRS3jr/3+TxoAXBHY6aXBGGDcNl3MVumnP/1p+k9//lfpw4cfpu0gSoCj2c2I3Vg3qb9CeeswTaa99PZbb6fhQGyBTQUAUn8DgfGgn548uyLVHc4wRe53ALCQIVWGExsV1zVI4ue28+Of6yYZnN9NMP4OAIA1CGbjje/g4ceMDLLEOnBYWwKWW2iT4HI4PJGRwz2AUk7mXAg2I2Ri6QCYEgGw4DuQeeJhAeaNwZZocS8Qpmh2uvMuA+pwiGjUCPLBAZHTiPt48uSZSgnYpRl/S+r6ezrmQYNnwUE8m10E9b2sSzoefYE/uGcyewB6wqEKRwWHOuYCGVYx6fR5OInzGdg9AhbRBIRdtfrqjgzHl+vHXe7AHNhITBgAHhhJdijU3VdaLGZM7iiUq9Jf2WBlyKhZEmUwBH6CpWIx+bK3BTQ7I41xYLkV2HVkaE54fbBQ4ZDCcVHm0sBf6TRbgj6zqsTWk63UfeHn2oYaAOS9o3QuTljppQ2zyLTAKK11MwEVMOyzzb0HupnOsvMt4s/P7wWdAmisvUax4wD5aukHsjI3Kud3oGd9To2tHe1u4FszOjVjBn8PA4Det3SmQqOotmn9ABLkbJm5STplznDj9wYAEUip6y06ualsxc/FZ7bmmLVDB2KKYl0BzJ1fXVKKAJlus3I5jsHczccOnN/oHOj9zHFxgsrNLch+QICJklo5d53nAwsiGBtg8wJck4HQuoUD7SSB1pDsjRnQZgrVzheBKzf6iP0HuwiHHV8Pu4r9SykGdPNdrTIAiBJg3IcBuP4WYH10BO8P0sn5lBo5eEEP6GQ8SR8+fJh1JOG4cvzHk/Tyyy+TgWuQHp9BCTDmBACu9UdxH9zDRwBA7+VPGgD0POSmF+5AXjFe2y60WSrvV8QAdBOkGgDk8nDXzQYA1N/YIaOzzvz7uplIXdIkxohAkOLnqlkT9hb8LtgBaDYRzAodStsgA0BZMuYfCQDYdlcupfeya1sw8hodtfocyNp9jXapB5+artGAoJ0QX1f7EVpkXWDfgUnXZnw0ALDV1q3tbH0fHwUAlC1Rgq8GAGmHXHZ6AADUOaXz1gCgrlM0addkm28SgDIwb77yzudonxZXzxg4j5MAjss5gJNVStNb/Hm2Q0J0kOZbMbA+/Vl11Rz15RMBANRLjCr+V32WRrLMpc3WD86MuNAEpBpDxQK05nkOkPM+Owzu9tbwA+EVB6NnPGKDEpyLlNgYmYGiBA0007i+QvLj6ZML3vc4GIubxZylpvOLqzifyzno+YCdNAA4RhFtPKvOT+j5Yk4M5IZ/G+Nj4AvnCvzM/uBEDLwxmq6M0nYYjRMiMwL7LsBUbC9KnZDJ5OeSPI1L6rHfxByVLjO0Z2FvRm4CFT4tgGucD0vaGTCpYt1Ht3P7ecuF4gqzJu3vuEQV9pP3z/MmGk6E1A7HaRZNbNYoVYU8jZKeOMe0P9Eg7iRNbqKiYJp6JxG3nqD532larFVpYYkM+qZMFEfjjNFS2r19+OW7tFnIJ9ms5VeidFn7oZQGe90yKQj2/WKRpgMk3QEQRRPCzZzfD3slXWONz3CgphNuqmc5oxYARLyC738eAIjxwH5gsp4ae+iKLOZgCwBC0kdxzo7+igH30XhK/wKNfNRsIxL+QYhwM6cs7eWYPKSBkBARYK1/AWy1zfnwN0sogAlKYDdiKzZBrHReASSzMih8WMhZIbGwXIYG5lbANjTBKcFTVbA4MUBbYim0sDK1je7aawGAi5WYrVvohlcAIErGKQfwSwQAa3t/SBKDvkFBuZTAdaLwBQDAfOYxNvjFAMDcbRxF79EjwPvC31P735lA9wIAoCRcdDVK0DQAIPby/wvSWMoN/E9+UwAAAABJRU5ErkJggg==";

        //private string spreadsheetPrinterSettingsPart2Data = "RgBvAHgAaQB0ACAAUgBlAGEAZABlAHIAIABQAEQARgAgAFAAcgBpAG4AdABlAHIAAAAAAAAAAAAAAAAAAAAAAAEEAQTcABQEX/+BBwEAAQDqCm8IZAABAAcAWAICAAEAWAICAAAATABlAHQAdABlAHIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAACAAAAAQAAAAEAAAABAAAAAAAAAAAAAAAAAAAAAAAAAEMAOgBcAFUAcwBlAHIAcwBcAHQAbwBtAGoAZQBiAG8AXABBAHAAcABEAGEAdABhAFwAUgBvAGEAbQBpAG4AZwBcAEYAbwB4AGkAdAAgAFMAbwBmAHQAdwBhAHIAZQBcAEYAbwB4AGkAdAAgAFAARABGACAAQwByAGUAYQB0AG8AcgBcAEYAbwB4AGkAdAAgAFIAZQBhAGQAZQByACAAUABEAEYAIABQAHIAaQBuAHQAZQByAFwAMQA1ADcAMAA1ADUANAA5ADgANwBfADkAOAAwADgAXwBfAGYAbwB4AGkAdAB0AGUAbQBwAC4AeABtAGwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        //private string spreadsheetPrinterSettingsPart3Data = "RgBvAHgAaQB0ACAAUgBlAGEAZABlAHIAIABQAEQARgAgAFAAcgBpAG4AdABlAHIAAAAAAAAAAAAAAAAAAAAAAAEEAQTcABQEX/+BBwEAAQDqCm8IZAABAAcAWAICAAEAWAICAAAATABlAHQAdABlAHIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAACAAAAAQAAAAEAAAABAAAAAAAAAAAAAAAAAAAAAAAAAEMAOgBcAFUAcwBlAHIAcwBcAHQAbwBtAGoAZQBiAG8AXABBAHAAcABEAGEAdABhAFwAUgBvAGEAbQBpAG4AZwBcAEYAbwB4AGkAdAAgAFMAbwBmAHQAdwBhAHIAZQBcAEYAbwB4AGkAdAAgAFAARABGACAAQwByAGUAYQB0AG8AcgBcAEYAbwB4AGkAdAAgAFIAZQBhAGQAZQByACAAUABEAEYAIABQAHIAaQBuAHQAZQByAFwAMQA1ADcAMAA1ADYAMAA1ADUAMABfADUANgAxADIAXwBfAGYAbwB4AGkAdAB0AGUAbQBwAC4AeABtAGwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        //private string spreadsheetPrinterSettingsPart4Data = "RgBvAHgAaQB0ACAAUgBlAGEAZABlAHIAIABQAEQARgAgAFAAcgBpAG4AdABlAHIAAAAAAAAAAAAAAAAAAAAAAAEEAQTcABQEX/+BBwEAAQDqCm8IZAABAAcAWAICAAEAWAICAAAATABlAHQAdABlAHIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAACAAAAAQAAAAEAAAABAAAAAAAAAAAAAAAAAAAAAAAAAEMAOgBcAFUAcwBlAHIAcwBcAHQAbwBtAGoAZQBiAG8AXABBAHAAcABEAGEAdABhAFwAUgBvAGEAbQBpAG4AZwBcAEYAbwB4AGkAdAAgAFMAbwBmAHQAdwBhAHIAZQBcAEYAbwB4AGkAdAAgAFAARABGACAAQwByAGUAYQB0AG8AcgBcAEYAbwB4AGkAdAAgAFIAZQBhAGQAZQByACAAUABEAEYAIABQAHIAaQBuAHQAZQByAFwAMQA1ADcAMAA1ADYAMAA1ADUAMABfADUANgAxADIAXwBfAGYAbwB4AGkAdAB0AGUAbQBwAC4AeABtAGwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        private System.IO.Stream GetBinaryDataStream(string base64String)
        {
            return new System.IO.MemoryStream(System.Convert.FromBase64String(base64String));
        }

    }
    // reportDataMapping classes
    public class reportDataObject
    {
        public Dictionary<string, userDetailsObject> people { get; set; }
        public tasksObject tasks { get; set; }
        public IList<bucketObject> buckets { get; set; }
        public string planId { get; set; }
        public string bucketId { get; set; }
        public string planTitle { get; set; }
        public string graphToken { get; set; }
        public userDetailsObject userDetails { get; set; }
        public string snappedImageUrl { get; set; }
        public string imageUrl { get; set; }
        public string channelName { get; set; }
        public string groupId { get; set; }
        public string smallPartImageData { get; set; }
        public string largePartImageData { get; set; }
    }
    public class userDetailsObject
    {
        [JsonPropertyName("@odata.context")]
        public string odataContext { get; set; }
        public IList<String> businessPhone { get; set; }
        public string displayName { get; set; }
        public string givenName { get; set; }
        public string jobTitle { get; set; }
        public string mail { get; set; }
        public string mobilePhone { get; set; }
        public string officeLocation { get; set; }
        public string preferredLanguage { get; set; }
        public string surname { get; set; }
        public string userPrincipalName { get; set; }
        public string id { get; set; }
    }
    public class tasksObject
    {
        [JsonPropertyName("@odata.context")]
        public string odataContext { get; set; }
        [JsonPropertyName("@odata.count")]
        public int dataCount { get; set; }
        public IList<taskObject> value { get; set; }

    }
    public class taskObject
    {
        [JsonPropertyName("@odata.etag")]
        public string odataEtag { get; set; }

        public string planId { get; set; }      //: "0wnpDRazhkmmFqbDTGyrj2UACGYj",
        public string bucketId { get; set; }      //: "bN2qlVl-f0Kxo8IzpYxZp2UAMvGr",
        public string title { get; set; }      //: "Painting broken",
        public string orderHint { get; set; }      //: "8586312100425054321",
        public string assigneePriority { get; set; }      //: "8586312100425054321",
        public int percentComplete { get; set; }     //: 0,
        public string startDateTime { get; set; }      //: null,
        public string createdDateTime { get; set; }      //: "2019-10-06T21:20:42.9721486Z",
        public string dueDateTime { get; set; }      //: "2019-10-07T07:00:00Z",
        public bool hasDescription { get; set; }      //: true,
        public string previewType { get; set; }      //: "noPreview",
        public string completedDateTime { get; set; }      //: null,
        public completedByObject completedBy { get; set; }      //: null,
        public int referenceCount { get; set; }     //: 0,
        public int checklistItemCount { get; set; }     //: 0,
        public int activeChecklistItemCount { get; set; }     //: 0,
        public string conversationThreadId { get; set; }      //: null,
        public string id { get; set; }      //: "KpZ1JQ7pBUK9_Vc5vkNaOGUALtPI",
        public createdByObject createdBy { get; set; }
        public appliedCategoriesObject appliedCategories { get; set; }

        public Dictionary<string, assignmentObject> assignments { get; set; }
    }
    public class assignmentObject
    {
        [JsonPropertyName("@odata.type")]
        public string odataType { get; set; }
        public string assignmentDateTime { get; set; }
        public string orderHint { get; set; }
        public userSimpleObject assignedBy { get; set; }
    }

    public class createdByObject
    {
        public userSimpleObject user { get; set; }
    }

    public class completedByObject
    {
        public userSimpleObject user { get; set; }
    }

    public class userSimpleObject
    {
        public string displayname { get; set; }
        public string id { get; set; }
    }

    public class appliedCategoriesObject
    {

    }

    public class bucketObject
    {
        [JsonPropertyName("@odata.etag")]
        public string odataEtag { get; set; }
        public string name { get; set; }
        public string planId { get; set; }
        public string orderHint { get; set; }
        public string id { get; set; }
    }
    public class WeatherForecast
    {
        public DateTimeOffset Date { get; set; }
        public int TemperatureC { get; set; }
        public string Summary { get; set; }
        public IList<DateTimeOffset> DatesAvailable { get; set; }
        public Dictionary<string, HighLowTemperatures> TemperatureRanges { get; set; }
        public string[] SummaryWords { get; set; }
    }

    public class HighLowTemperatures
    {
        public Temperature High { get; set; }
        public Temperature Low { get; set; }
    }

    public class Temperature
    {
        public int DegreesCelsius { get; set; }
    }
}