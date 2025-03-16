using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Runtime.InteropServices;
using Kuyumcu.Models;

namespace Kuyumcu.Services
{
    public class PrintService
    {
        private const int ThermalPaperWidth = 58; // 58mm thermal printer width
        private const int CharactersPerLine = 32; // Typical characters for 58mm printer
        
        // Yazıcı adı belirtilmezse varsayılan yazıcı kullanılır
        private string? _printerName;
        
        // Yazıcı adını ayarlamak için metod
        public void SetPrinterName(string printerName)
        {
            _printerName = printerName;
        }
        
        public void PrintCustomerReceipt(Customer customer, List<TransactionGroup> transactions)
        {
            try
            {
                PrintDocument printDoc = new PrintDocument();
                
                // Özel sayfa boyutu ayarlama (58mm termal yazıcı)
                printDoc.DefaultPageSettings.PaperSize = new PaperSize("Custom", 220, 800); // ~58mm width
                
                // Yazıcıyı doğrudan belirle (diyalog göstermeden)
                if (!string.IsNullOrEmpty(_printerName))
                {
                    printDoc.PrinterSettings.PrinterName = _printerName;
                }
                
                // Print diyalogunu gösterme
                printDoc.PrinterSettings.PrintToFile = false;
                
                // Sayfa içeriğini hazırla
                printDoc.PrintPage += (sender, e) => PrintPage_CustomerReceipt(sender, e, customer, transactions);
                
                // Doğrudan yazdır
                printDoc.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Printing error: {ex.Message}");
                throw; // Hatayı yukarı fırlat, CustomerDetail sayfasında gösterilecek
            }
        }

        private void PrintPage_CustomerReceipt(object sender, PrintPageEventArgs e, Customer customer, List<TransactionGroup> transactions)
        {
            // Setup
            Graphics graphics = e.Graphics;
            System.Drawing.Font regularFont = new System.Drawing.Font("Arial", 8);
            System.Drawing.Font boldFont = new System.Drawing.Font("Arial", 8, FontStyle.Bold);
            System.Drawing.Font titleFont = new System.Drawing.Font("Arial", 10, FontStyle.Bold);
            int yPos = 10;
            int leftMargin = 5;
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            
            // Title
            float titleWidth = e.PageBounds.Width - (leftMargin * 2);
            RectangleF titleRect = new RectangleF(leftMargin, yPos, titleWidth, 20);
            graphics.DrawString("MÜŞTERİ DURUM MAKBUZU", titleFont, Brushes.Black, titleRect, centerFormat);
            yPos += 25;
            
            // Date and Time
            string dateTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            graphics.DrawString($"Tarih: {dateTime}", regularFont, Brushes.Black, leftMargin, yPos);
            yPos += 20;
            
            // Separator
            DrawSeparator(graphics, leftMargin, yPos, e.PageBounds.Width - (leftMargin * 2));
            yPos += 10;
            
            // Customer Info
            graphics.DrawString($"Müşteri: {customer.Name}", boldFont, Brushes.Black, leftMargin, yPos);
            yPos += 15;
            
            //if (!string.IsNullOrEmpty(customer.PhoneNumber))
            //{
            //    graphics.DrawString($"Tel: {customer.PhoneNumber}", regularFont, Brushes.Black, leftMargin, yPos);
            //    yPos += 15;
            //}
            
            // Transaction Summary
            graphics.DrawString("GÜNCEL DURUM", boldFont, Brushes.Black, leftMargin, yPos);
            yPos += 20;
            
            if (transactions != null && transactions.Any())
            {
                foreach (var group in transactions)
                {
                    string balanceText;
                    System.Drawing.Brush balanceBrush = Brushes.Black;
                    
                    if (group.Balance == 0)
                    {
                        balanceText = "Hesap Sıfır";
                    }
                    else if (group.Balance > 0)
                    {
                        balanceText = $"{group.Balance:0.##} {group.Key.GetSymbol()} (Borcum)";
                        balanceBrush = Brushes.Black; // Use default black for thermal printer
                    }
                    else
                    {
                        balanceText = $"{Math.Abs(group.Balance):0.##} {group.Key.GetSymbol()} (Alacak)";
                        balanceBrush = Brushes.Black; // Use default black for thermal printer
                    }
                    
                    graphics.DrawString($"{group.Key.GetDisplayName()}:", regularFont, Brushes.Black, leftMargin, yPos);
                    
                    // Calculate text width to align values to the right
                    System.Drawing.SizeF textSize = graphics.MeasureString(balanceText, regularFont);
                    float rightPos = e.PageBounds.Width - leftMargin - textSize.Width;
                    
                    graphics.DrawString(balanceText, regularFont, balanceBrush, rightPos, yPos);
                    yPos += 15;
                }
            }
            else
            {
                graphics.DrawString("İşlem kaydı bulunamadı.", regularFont, Brushes.Black, leftMargin, yPos);
                yPos += 15;
            }
            
            // Footer
            yPos += 10;
            DrawSeparator(graphics, leftMargin, yPos, e.PageBounds.Width - (leftMargin * 2));
            yPos += 10;
            
            string footerText = "Kuyumcu Mehmet - 0535 292 1730";
            System.Drawing.SizeF footerSize = graphics.MeasureString(footerText, regularFont);
            float footerX = (e.PageBounds.Width - footerSize.Width) / 2;
            graphics.DrawString(footerText, regularFont, Brushes.Black, footerX, yPos);
            
            // No more pages to print
            e.HasMorePages = false;
        }
        
        private void DrawSeparator(Graphics graphics, int x, int y, float width)
        {
            graphics.DrawLine(Pens.Black, x, y, x + width, y);
        }
        
        // Sistemdeki mevcut yazıcıları getirmek için yardımcı metod
        public List<string> GetAvailablePrinters()
        {
            var printers = new List<string>();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                printers.Add(printer);
            }
            return printers;
        }
    }
}
