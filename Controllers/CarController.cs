using AngleSharp;
using CarApi.Models;
using Microsoft.AspNetCore.Mvc;
using WooCommerceNET;
using WooCommerceNET.WooCommerce.v3;

namespace CarApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CarController : ControllerBase
    {

        private readonly string _consumerKey = "ck_183bef4acc2f2e765dc4cd55a5e090a10a766360";
        private readonly string _consumerSecret = "cs_f770bcc6a78de8da8ad82f99ab3a130685aa9567";
        private readonly string _storeUrl = "http://test-woocomerce.local/";

        [HttpGet]
        public async Task<ActionResult<Car>> GetCarDetails(string url)
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(url);

            var nameElement = document.QuerySelector(".offer-title");
            var name = nameElement != null ? nameElement.TextContent.Trim() : "Name not found";

            var descriptionElement = document.QuerySelector(".e9na3zb2");
            var description = descriptionElement != null ? descriptionElement.TextContent.Trim() : "Description not found";
            var cleanedDescription = System.Text.RegularExpressions.Regex.Replace(description, @"\{.*?\}|\.[-\w]+", string.Empty).Trim();

            var car = new Car
            {
                Url = url,
                Name = name,
                Details = new Dictionary<string, string>(),
                Description = cleanedDescription,
            };

            var detailsSection = document.QuerySelector(".ooa-w4tajz");


            if (detailsSection != null)
            {
                var elements = detailsSection.QuerySelectorAll("p, a");
                var pairedElements = new List<string>();
                bool stopCollecting = false;

                foreach (var item in elements)
                {
                    if (stopCollecting)
                    {
                        break; // Stop the loop if we've set stopCollecting to true
                    }

                    string text = item.TextContent.Trim();
                    pairedElements.Add(text);

                    if (pairedElements.Count == 2)
                    {
                        // Use the first text as the key and the second as the value
                        car.Details[pairedElements[0]] = pairedElements[1];
                        if (pairedElements[0] == "Stan")
                        {
                            stopCollecting = true; // Set the flag to true to stop collecting after "Stan"
                        }
                        pairedElements.Clear();
                    }
                }

                // Handle any remaining element if the total count is odd
                if (pairedElements.Count > 0 && !stopCollecting)
                {
                    Console.WriteLine("Last key without a value: " + pairedElements[0]); // Or handle the last key differently
                }
            }
            else
            {
                Console.WriteLine("Details section not found.");
            }

            return car;
        }

        [HttpPost]
        public async Task<IActionResult> AddCarToWooCommerce([FromForm] Car car)
        {
            if (car == null)
            {
                return BadRequest("Invalid car data");
            }

            // Logika do obsługi przesyłanych obrazów
            var imageUrls = new List<string>();
            if (car.Images != null)
            {
                foreach (var image in car.Images)
                {
                    if (image.Length > 0)
                    {
                        // Tu możesz zapisać pliki na serwerze lub w chmurze i zwrócić ich URL-e
                        var imageUrl = await SaveImageAndGetUrl(image);
                        imageUrls.Add(imageUrl);
                    }
                }
            }

            await AddCarAsProduct(car);
            return Ok(car.Images);
        }

        private async Task<Product> AddCarAsProduct(Car car)
        {
            var wc = new WCObject(new RestAPI($"{_storeUrl}wp-json/wc/v3/", _consumerKey, _consumerSecret));

            var product = new Product
            {
                name = car.Name,
                type = "simple",
                description = car.Description,
                regular_price = 0, // You may need to convert this to a decimal or double value
                attributes = new List<ProductAttributeLine>()
            };

            // Add details as product attributes
            foreach (var detail in car.Details)
            {
                product.attributes.Add(new ProductAttributeLine
                {
                    name = detail.Key,
                    visible = true,
                    variation = false,
                    options = new List<string> { detail.Value }
                });
            }

            // Add images
            var imageUrls = new List<ProductImage>();
            if (car.Images != null)
            {
                foreach (var image in car.Images)
                {
                    if (image != null && image.Length > 0)
                    {
                        var imageUrl = await SaveImageAndGetUrl(image);
                        imageUrls.Add(new ProductImage { src = "https://cdn.pixabay.com/photo/2018/01/14/23/12/nature-3082832_1280.jpg" });
                    }
                }
            }
            product.images = imageUrls;

            try
            {
                var createdProduct = await wc.Product.Add(product);
                Console.WriteLine($"Product created successfully! ID: {createdProduct.id}");
                return createdProduct;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create the product: {ex.Message}");
                return null;
            }
        }


        private async Task<string> SaveImageAndGetUrl(IFormFile image)
        {
            var imagePath = Path.Combine("wwwroot/images", image.FileName);

            // Utwórz katalog, jeśli nie istnieje
            var directoryPath = Path.GetDirectoryName(imagePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (var stream = new FileStream(imagePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Zwróć URL do obrazu
            return $"{Request.Scheme}://{Request.Host}/images/{image.FileName}";
        }
    }
}
