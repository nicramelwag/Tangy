using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tangy.Data;
using Tangy.Models;
using Tangy.Models.OrderDetailsViewModel;
using Tangy.Services;
using Tangy.Utility;

namespace Tangy.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private int PageSize = 2;

        public OrderController(ApplicationDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;

        }

        [Authorize]
        public async Task<IActionResult> Confirm(int id)
        {
            var claimsIdentity = (ClaimsIdentity)this.User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            OrderDetailsViewModel orderDetailsViewModel = new OrderDetailsViewModel()
            {
                OrderHeader = await _db.OrderHeader.Where(o => o.Id == id && o.UserId == claim.Value).FirstOrDefaultAsync(),
                OrderDetail = _db.OrderDetails.Where(o => o.OrderId == id).ToList()
            };

            var customerEmail = _db.Users.Where(u => u.Id == orderDetailsViewModel.OrderHeader.UserId).FirstOrDefault().Email;
            await _emailSender.SendOrderStatusAsync(customerEmail, orderDetailsViewModel.OrderHeader.Id.ToString(), SD.StatusSubmitted);

            return View(orderDetailsViewModel);
        }

        [Authorize]
        public IActionResult OrderHistory(int productPage =1)
        {
            var claimsIdentity = (ClaimsIdentity)this.User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            OrderListViewModel orderListViewModel = new OrderListViewModel()
            {
                Orders = new List<OrderDetailsViewModel>()
            };

            List<OrderHeader> orderHeaderList = _db.OrderHeader.Where(o => o.UserId == claim.Value).OrderByDescending(u => u.OrderDate).ToList();

            foreach (var item in orderHeaderList)
            {
                OrderDetailsViewModel individul = new OrderDetailsViewModel()
                {
                    OrderHeader = item,
                    OrderDetail = _db.OrderDetails.Where(o => o.OrderId == item.Id).ToList()
                };

                orderListViewModel.Orders.Add(individul);
            }
            var count = orderListViewModel.Orders.Count;
            orderListViewModel.Orders = orderListViewModel.Orders.OrderBy(p => p.OrderHeader.Id)
                .Skip((productPage - 1) * PageSize).Take(PageSize).ToList();


            orderListViewModel.PagingInfo = new PagingInfo()
            {
                CurrentPage = productPage,
                ItemsPerPage = PageSize,
                TotalItem = count
            };

            return View(orderListViewModel);
        }

        [Authorize(Roles = SD.AdminEndUser)]
        public IActionResult ManageOrder()
        {
            List<OrderDetailsViewModel> orderDetailsVM = new List<OrderDetailsViewModel>();

            List<OrderHeader> orderHeaderList = _db.OrderHeader.Where(o => o.Status == SD.StatusSubmitted || o.Status == SD.StatusInProcess).OrderBy(u => u.PickUpTime).ToList();

            foreach (var item in orderHeaderList)
            {
                OrderDetailsViewModel individul = new OrderDetailsViewModel()
                {
                    OrderHeader = item,
                    OrderDetail = _db.OrderDetails.Where(o => o.OrderId == item.Id).ToList()
                };

                orderDetailsVM.Add(individul);
            }

            return View(orderDetailsVM);
        }

        [Authorize (Roles = SD.AdminEndUser)]
        public async Task<IActionResult> OrderPrepare(int orderId)
        {
            OrderHeader orderHeader = _db.OrderHeader.Find(orderId);
            orderHeader.Status = SD.StatusInProcess;
            await _db.SaveChangesAsync();
            return RedirectToAction("ManageOrder", "Order");
        }

        [Authorize(Roles = SD.AdminEndUser)]
        public async Task<IActionResult> OrderCancel(int orderId)
        {
            OrderHeader orderHeader = _db.OrderHeader.Find(orderId);
            orderHeader.Status = SD.StatusCancelled;
            await _db.SaveChangesAsync();
            var customerEmail = _db.Users.Where(u => u.Id == orderHeader.UserId).FirstOrDefault().Email;
            await _emailSender.SendOrderStatusAsync(customerEmail, orderHeader.Id.ToString(), SD.StatusCancelled);
            return RedirectToAction("ManageOrder", "Order");
        }

        [Authorize(Roles = SD.AdminEndUser)]
        public async Task<IActionResult> OrderReady(int orderId)
        {
            OrderHeader orderHeader = _db.OrderHeader.Find(orderId);
            orderHeader.Status = SD.StatusReady;
            await _db.SaveChangesAsync();
            var customerEmail = _db.Users.Where(u => u.Id == orderHeader.UserId).FirstOrDefault().Email;
            await _emailSender.SendOrderStatusAsync(customerEmail, orderHeader.Id.ToString(), SD.StatusReady);
            return RedirectToAction("ManageOrder", "Order");
        }

        public IActionResult OrderPickup(string searchOrder, string searchPhone, string searchEmail)
        {
            List<OrderDetailsViewModel> orderDetailsVM = new List<OrderDetailsViewModel>();
            
            if (searchEmail != null || searchOrder != null || searchPhone != null)
            {
                var user = new ApplicationUser();
                List<OrderHeader> orderHeaders = new List<OrderHeader>();

                if (searchOrder != null)
                {
                    orderHeaders = _db.OrderHeader.Where(o => o.Id == Convert.ToInt32(searchOrder)).ToList();
                }
                else
                {
                    if (searchEmail != null)
                    {
                        user = _db.ApplicationUser.Where(u => u.Email.ToLower().Contains(searchEmail.ToLower())).FirstOrDefault();
                    }
                    else
                    {
                        if (searchPhone != null)
                        {
                            user = _db.ApplicationUser.Where(u => u.PhoneNumber.Contains(searchPhone)).FirstOrDefault();
                        }
                    }
                }

                if(user != null || orderHeaders.Count > 0)
                {
                    if (orderHeaders.Count == 0)
                    {
                        orderHeaders = _db.OrderHeader.Where(o => o.UserId == user.Id).OrderByDescending(o => o.OrderDate).ToList();
                    }

                    foreach (OrderHeader item in orderHeaders)
                    {
                        OrderDetailsViewModel individul = new OrderDetailsViewModel()
                        {
                            OrderHeader = item,
                            OrderDetail = _db.OrderDetails.Where(o => o.OrderId == item.Id).ToList()
                        };

                        orderDetailsVM.Add(individul);
                    }
                }
            }
            else
            {

                List<OrderHeader> orderHeaderList = _db.OrderHeader.Where(o => o.Status == SD.StatusReady).OrderByDescending(u => u.PickUpTime).ToList();

                foreach (var item in orderHeaderList)
                {
                    OrderDetailsViewModel individul = new OrderDetailsViewModel()
                    {
                        OrderHeader = item,
                        OrderDetail = _db.OrderDetails.Where(o => o.OrderId == item.Id).ToList()
                    };

                    orderDetailsVM.Add(individul);
                }
            }


            return View(orderDetailsVM);
        }

        [Authorize(Roles =SD.AdminEndUser)]
        public IActionResult OrderPickupDetails(int orderId)
        {
            OrderDetailsViewModel orderDetailsVM = new OrderDetailsViewModel()
            {
                OrderHeader = _db.OrderHeader.Where(o => o.Id == orderId).FirstOrDefault()
            };

            orderDetailsVM.OrderHeader.ApplicationUser = _db.ApplicationUser.Where(u => u.Id == orderDetailsVM.OrderHeader.UserId).FirstOrDefault();

            orderDetailsVM.OrderDetail = _db.OrderDetails.Where(o => o.OrderId == orderDetailsVM.OrderHeader.Id).ToList();

            return View(orderDetailsVM);

        }

        [Authorize(Roles = SD.AdminEndUser)]
        [HttpPost]
        [ActionName("OrderPickupDetails")]
        public async Task<IActionResult> OrderPickupDetailsPost(int orderId)
        {
            OrderHeader orderHeader = _db.OrderHeader.Find(orderId);
            orderHeader.Status = SD.StatusCompleted;
            await _db.SaveChangesAsync();
            return RedirectToAction("OrderPickup", "Order");
        }

        [Authorize(Roles = SD.AdminEndUser)]
        public IActionResult OrderSummaryExport()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = SD.AdminEndUser)]
        public IActionResult OrderSummaryExport(OrderExportViewModel exportViewModel)
        {

            List<OrderHeader> orderHeaderList = _db.OrderHeader
                .Where(o => o.OrderDate >= exportViewModel.startDate && o.OrderDate <= exportViewModel.endDate)
                .OrderByDescending(u => u.OrderDate).ToList();

            List<OrderDetails> orderDetails = new List<OrderDetails>();

            foreach (var item in orderHeaderList)
            {
                orderDetails.AddRange(_db.OrderDetails.Where(o => o.OrderId == item.Id).ToList());
            }

            byte[] bytes = Encoding.ASCII.GetBytes(ConvertToString(orderDetails));
            return File(bytes, "application/text", "OrderDetail.csv");
        }

        public String ConvertToString<T>(IList<T> data)
        {

            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            foreach (PropertyDescriptor prop in properties)
            {
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }
            foreach (T item in data)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                {
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }
            table.Columns.Remove("OrderHeader");
            table.Columns.Remove("MenuItemId");
            table.Columns.Remove("MenuItem");
            table.Columns.Remove("Description");

            StringBuilder sb = new StringBuilder();

            IEnumerable<string> columnNames = table.Columns.Cast<DataColumn>().
                                              Select(column => column.ColumnName);
            sb.AppendLine(string.Join(",", columnNames));
            foreach (DataRow row in table.Rows)
            {
                IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                sb.AppendLine(string.Join(",", fields));
            }
            return sb.ToString();
        }
    }
}