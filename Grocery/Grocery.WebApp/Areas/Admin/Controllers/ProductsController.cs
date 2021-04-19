using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Grocery.WebApp.Areas.Admin.ViewModels;
using Grocery.WebApp.Data;
using Grocery.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Grocery.WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize] // only a user who is authenticated and authorized is allowed to access this
    public class ProductsController : Controller
    {

        private const string BlobContainerNAME = "myimages"; //name of container must be all lowercase

        private readonly ApplicationDbContext _context; // use to retrieve records pointing to table
        private readonly ILogger<ProductsController> _logger; // readonly = no other method can modify this
        private readonly UserManager<MyIdentityUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _environment;


        // through DI, all the objects are initialized for you
        public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger, UserManager<MyIdentityUser> userManager, IConfiguration config, IWebHostEnvironment environment)
        {   
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _config = config;
            _environment = environment;
        }


        // GET: ProductsController
        public ActionResult Index() //list all data from db
        {

            // 1. LAMBDA version to extract all products using Eager Loading
            // give all records from the products table in db
            // also get the data of child records (parent-child relationships)

            try {

            var products = _context.Products
                            .Include(p => p.CreatedByUser) // include createdbyuser , add include, becomes eager loading
                            .Include(p => p.UpdatedByUser)
                            .ToList();
            
            //foreach (var p in products)
            //{
            //    Console.WriteLine(p.ProductID);
            //    Console.WriteLine(p.ProductName);
            //    Console.WriteLine(p.CreatedByUser.DisplayName);
            //}

            // projection done manually here as 2nd step
            // bind data of the model to the viewmodel (called model binding)

            List<ProductViewModel> productViewModels = new List<ProductViewModel>();

            foreach (var p in products)
            {
                productViewModels.Add(new ProductViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    SellingPricePerUnit = p.SellingPricePerUnit,
                    Quantity = p.Quantity,
                    BlobImageURL = p.BlobImageURL,

                    CreatedByUser = p.CreatedByUser,
                    CreatedByUserId = p.CreatedByUserId,
                    UpdatedByUser = p.UpdatedByUser,
                    UpdatedByUserId = p.UpdatedByUserId,
                    LastUpdatedOn = p.LastUpdatedOn
                });
            }

                return View(productViewModels);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Index: " + ex);
                return View();
            }
            
        }


        // 2. LINQ version to extract all products using Eager Loading
        // and project the data into the viewmodel (powerfeature of linq, all in 1 step)
        // p = the value you are pulling from your db

        //var productViewModels = (from p in _context.Products.Include(p => p.CreatedByUser).Include(p => p.UpdatedByUser)
        //                         select new ProductViewModel
        //                         {
        //                             ProductID = p.ProductID,
        //                             ProductName = p.ProductName,
        //                             SellingPricePerUnit = p.SellingPricePerUnit,
        //                             Quantity = p.Quantity,
        //                             Image = p.Image,

        //                             LastUpdatedOn = p.LastUpdatedOn,
        //                             CreatedByUser = p.CreatedByUser,
        //                             CreatedByUserId = p.CreatedByUserId,
        //                             UpdatedByUser = p.UpdatedByUser,
        //                             UpdatedByUserId = p.UpdatedByUserId
        //                         }).ToList();



        // GET: ProductsController/Details/5
        public async Task<ActionResult> Details(Guid? id)
        {

            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FirstOrDefaultAsync(m => m.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }


            // initialize the  viewmodel
            // do this to show the values of the fields on the details page
            ProductViewModel productViewModel = new ProductViewModel()
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                SellingPricePerUnit = product.SellingPricePerUnit,
                Quantity = product.Quantity,
                //Image = product.Image // need to get from azure db rather than product table in sql server db
                BlobImageURL = product.BlobImageURL
            };
                


            return View(productViewModel); //render details page, pass in values in productviewmodel
      
        }

        // GET: ProductsController/Create
        public ActionResult Create() // called when click create new listing
        {
            ProductViewModel productViewModel = new ProductViewModel();

            return View(productViewModel); // pass empty pvm to the view
        }



        // bind -> adding an attribute to productviewmodel
        // model binding, properties submitted from browser to server, I am expecting a field called productid, etc...
        // define what are the fields a user can submit to the server (define the list of fields that can be received)
        // replace IformCollection collection with [Bind("ProductID,ProductName,Quantity,SellingPricePerUnit,Image,CreatedByUserId,UpdatedByUserId,LastUpdatedOn")] Product product


        //no need to bind imagefile, it will come to us through an attachment, accessed through a different way.



        // POST: ProductsController/Create
        // need to wrap ActionResult in Task -> multi-tasking multi-threaded code
        [HttpPost]
        [ValidateAntiForgeryToken] //takes care of cross-side script attacks (protect from XSRF attack = hacking process), will not allow cross-side post on your server
        public async Task<ActionResult> Create([Bind("ProductName,SellingPricePerUnit,Quantity")] ProductViewModel productViewModel) //receive collection of form objects from Create.cshtml
        {   
          
            var user = await _userManager.GetUserAsync(User); // User -> pass in the current logged in user

            if (user == null)
            {
                ModelState.AddModelError("Create", "User not found... please log back in...");
            }


            if (!ModelState.IsValid)
            {
                return View(productViewModel); // if invalid, go back same page and display all the error msg
            }

 
            //upload image file to azure blob storage
            try
            {
                IFormFile file = Request.Form.Files.FirstOrDefault();

                // Check if file was uploaded, and is not an empty file.
                if (file != null && file.Length > 0) // and if file > 0 bytes
                {
                    // 1. Save the uploaded file on a temporary "UploadedImages" folder in wwwroot.
                    var filepath = Path.Combine(_environment.WebRootPath, "UploadedImages", file.FileName);

                    using (var stream = System.IO.File.Create(filepath))
                    {
                        //file.CopyToAsync(stream).Wait(); //forcefully make synchronous, so that next line wont execute until this line finishes
                        await file.CopyToAsync(stream);
                    }


                    // 2. Upload the image to the Blob Container
                    string imgBlobUri = await this.fSaveToBlobStorageAsync(file.FileName, filepath);


                    // Console.WriteLine("imgBlobUri value: " + imgBlobUri);
                    // imgBlobUri value: https://ywlstorage.blob.core.windows.net/myimages/vv1.png



                    // 3. Delete the uploaded image file from the temporary folder, as not needed any more.
                    System.IO.File.Delete(filepath);


                    //var imgLink = $"<a href='{imgBlobUri}' target='_blank'>{imgBlobUri}</a>";
                    //ViewBag.StatusType = "success";
                    //ViewBag.StatusMessage = $"Saved file successfully to:<br />{imgLink}";



                    Product newProduct = new Product()
                    {
                        ProductID = new Guid(),
                        ProductName = productViewModel.ProductName,
                        SellingPricePerUnit = productViewModel.SellingPricePerUnit,
                        Quantity = productViewModel.Quantity,
                        //Image = null, // for image -> store null into db
                        BlobImageURL = imgBlobUri,

                        LastUpdatedOn = DateTime.Now,
                        CreatedByUserId = user.Id                //need to inject userManager to access user
                    };


                    _context.Products.Add(newProduct);
                    _context.SaveChanges(); //commit changes to db

                    //return RedirectToAction("Index");
                    return RedirectToAction(nameof(Index));

                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Create: " + ex);
                return View(productViewModel);
            }


            return View(productViewModel);

        }


        //check if the file has been attached while submitting the form
        // if at least 1 file is attached, we try to access it.

        /* upload image file to database
        if(Request.Form.Files.Count >= 1)
        {
            //shortcut method, in some cases wont work
            //IFormFile file = productViewModel.ImageFile;

            //extract the file you want to 
            IFormFile file = Request.Form.Files.FirstOrDefault();

            //copy the file uploaded using the momorystream - into the Product.Image
            using (var dataStream = new MemoryStream())
            {
                await file.CopyToAsync(dataStream); // copy into datastream
                newProduct.Image = dataStream.ToArray();
            }
        }
        */



        // GET: ProductsController/Edit/5
        // add ? -> b/c user might not have passed anything
        public async Task<ActionResult> Edit(Guid? id) //change id to guid, the id it is going to receive is a guid
        {

            if (id == null)
            {
                return NotFound();
            }

            // get data from db -> find product whose id matches, assign to producttoedit
            Product productToEdit = await _context.Products.FindAsync(id);

            if (productToEdit == null)
            {
                return NotFound();
            }

            //initialize the  viewmodel
            ProductViewModel productViewModel = new ProductViewModel()
            {
                ProductID = productToEdit.ProductID,
                ProductName = productToEdit.ProductName,
                SellingPricePerUnit = productToEdit.SellingPricePerUnit,
                Quantity = productToEdit.Quantity,
                //Image = productToEdit.Image,
                BlobImageURL = productToEdit.BlobImageURL,

                LastUpdatedOn = productToEdit.LastUpdatedOn,
                CreatedByUser = productToEdit.CreatedByUser,
                CreatedByUserId = productToEdit.CreatedByUserId,
                UpdatedByUser = productToEdit.UpdatedByUser,
                UpdatedByUserId = productToEdit.UpdatedByUserId
            };

            return View(productViewModel); // render edit page, pass in productViewModel into the page to display fields
        }

        // POST: ProductsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ProductID,ProductName,Quantity,SellingPricePerUnit,BlobImageURL")] ProductViewModel productViewModel) // after form submission on edit page, goes to this function
        {

            var user = await _userManager.GetUserAsync(User); 

            if (user == null)
            {
                ModelState.AddModelError("Create", "User not found.  Please log back in!");
            }

            if (!ModelState.IsValid)
            {
                return View(productViewModel);
            }



            // find the product you want to edit
            Product editProduct = await _context.Products.FindAsync(productViewModel.ProductID);

            if (editProduct == null)
            {
                return NotFound();
            }


            //update properties of model - from the viewmodel
            editProduct.ProductName = productViewModel.ProductName;
            editProduct.SellingPricePerUnit = productViewModel.SellingPricePerUnit;
            editProduct.Quantity = productViewModel.Quantity;
            editProduct.BlobImageURL = productViewModel.BlobImageURL;

            editProduct.LastUpdatedOn = DateTime.Now;
            editProduct.UpdatedByUserId = user.Id;


            //upload image file to azure blob storage
            try
            {
                IFormFile file = Request.Form.Files.FirstOrDefault();

                // Check if file was uploaded, and is not an empty file.
                if (file != null && file.Length > 0) // and if file > 0 bytes
                {

                    // Delete 
                    if (editProduct.BlobImageURL != null)
                    {
                        string blobName = editProduct.BlobImageURL.Substring(editProduct.BlobImageURL.LastIndexOf("/") + 1);

                        Console.WriteLine("blobName: " + blobName);

                        await this.fDeleteFromBlobStorageAsync(blobName); // delete image from blob storage
                    }


                    // 1. Save the uploaded file on a temporary "UploadedImages" folder in wwwroot.
                    var filepath = Path.Combine(_environment.WebRootPath, "UploadedImages", file.FileName);

                    using (var stream = System.IO.File.Create(filepath))
                    {
                        //file.CopyToAsync(stream).Wait(); //forcefully make synchronous, so that next line wont execute until this line finishes
                        await file.CopyToAsync(stream);
                    }

                    
                    // 2. Upload the image to the Blob Container
                    string imgBlobUri = await this.fSaveToBlobStorageAsync(file.FileName, filepath);
                    editProduct.BlobImageURL = imgBlobUri;


                    // Console.WriteLine("imgBlobUri value: " + imgBlobUri);
                    // imgBlobUri value: https://ywlstorage.blob.core.windows.net/myimages/vv1.png



                    // 3. Delete the uploaded image file from the temporary folder, as not needed any more.
                    System.IO.File.Delete(filepath);

                }

       
                _context.Products.Update(editProduct); //update the database, update command, not add command
                _context.SaveChanges();
                return RedirectToAction(nameof(Index)); //render index page
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Edit: " + ex);
                return View(productViewModel);
            }
        }


        //replaced with above
        //Product newProduct = new Product() //assign value from pvm
        //{
        //    ProductID = new Guid(),
        //    ProductName = productViewModel.ProductName,
        //    SellingPricePerUnit = productViewModel.SellingPricePerUnit,
        //    Quantity = productViewModel.Quantity,

        //    LastUpdatedOn = DateTime.Now,
        //    CreatedByUserId = user.Id
        //};


        // check if file has attached while submitting the Form
        //if (Request.Form.Files.Count >= 1)
        //{
        //    IFormFile file = Request.Form.Files.FirstOrDefault();
        //    // IFormFile file = productViewModel.ImageFile;

        //    // copy the file uploaded using the MemoryStream - into the Product.Image
        //    using (var dataStream = new MemoryStream())
        //    {
        //        await file.CopyToAsync(dataStream);
        //        editProduct.Image = dataStream.ToArray();
        //    }
        //}


        // GET: ProductsController/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {

            if (id == null)
            {
                return NotFound();
            }

            Product productToDelete = await _context.Products.FindAsync(id);

            if (productToDelete == null)
            {
                return NotFound();
            }


            // do this to show the values of the fields on the delete page
            ProductViewModel productViewModel = new ProductViewModel()
            {
                ProductID = productToDelete.ProductID,
                ProductName = productToDelete.ProductName,
                SellingPricePerUnit = productToDelete.SellingPricePerUnit,
                Quantity = productToDelete.Quantity,
                BlobImageURL = productToDelete.BlobImageURL
            };


            return View(productViewModel); // render delete page, pass in productviewmodel
        }


        // POST: ProductsController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteAsync(Guid id) // after form submission on delete page, code goes here
        { 

            // find the product you want to edit
            Product productToDelete = await _context.Products.FindAsync(id);

            if (productToDelete == null)
            {
                return NotFound();
            }

            try
            {
                if (productToDelete.BlobImageURL != null)
                {
                    string blobName = productToDelete.BlobImageURL.Substring(productToDelete.BlobImageURL.LastIndexOf("/") + 1);

                    Console.WriteLine("blobName: " + blobName);

                    await this.fDeleteFromBlobStorageAsync(blobName); // delete image from blob storage
                }


                _context.Products.Remove(productToDelete);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Delete: " + ex);
                return View();
            }
        }


        #region Helper Methods


        private async Task<string> fSaveToBlobStorageAsync(string blobName, string filePath)
        {
            var storageConn = _config.GetValue<string>("AppSettings:MyAzureStorageConnectionKey1");

            // Get a reference to a Container
            BlobContainerClient blobContainerClient = new BlobContainerClient(storageConn, BlobContainerNAME);

            // Create the container if it does not exist - and granting PUBLIC access at the container level.
            //await blobContainerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);

            // Upload the file
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(filePath); // this method uploads file

            // Return the URL of the file on successful upload.
            return blobClient.Uri.AbsoluteUri; //retrieve the url of file so that I can give to someone and they can access it
        }


        private async Task fDeleteFromBlobStorageAsync(string blobName)
        {

            var storageConn = _config.GetValue<string>("AppSettings:MyAzureStorageConnectionKey1");

            // Get a reference to a Container
            BlobContainerClient blobContainerClient = new BlobContainerClient(storageConn, BlobContainerNAME);

            // Delete the file
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();


            Console.WriteLine(blobName + ": file deleted successfully from azure blob storage...");

        }


        #endregion

    }
}
