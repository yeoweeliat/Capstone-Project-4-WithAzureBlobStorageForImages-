


Notes:

1. @ -> indicates this is c# code running on the webpage (eg Index.cshtml)

2. @page -> indicates this is a razor page

3. @model IndexModel -> IndexModel is the data associated with this page

- this is like webforms (where you have webcode in the frontend, and c# code that will run in the background and do c# work [codebehind])


4. Use depending on the circumstance, what you need:

- Razor Syntax Inline: @model IndexModel,  @page

- Multi-line:

@{
	ViewData["Title"] = "Home Page"; //rendered on [Shared >> _Layout.cshtml], head, title section
	// viewdata is a dictionary(dumping ground) where you put key value pairs

}


5.


 



you can create as many solution folders as you want. 


multipleactiveresultset
pattern of development = more than 1 set of data you can return to application. allow app to receive multiple results froms tore application/review


Support for ASP.NET Core Identity was added to your project.

For setup and configuration information, see https://go.microsoft.com/fwlink/?linkid=2116645.



Backup code:

<!-- upload image to db -->

            <div class="form-group">
                <label asp-for="ImageFile" class="control-label"></label>
                @if (Model.Image != null)
                {
                    @*@Html.DisplayFor(modelItem => item.Image)*@
                    <img id="myImage" class="myProductImage" src="data:image/*;base64,@(Convert.ToBase64String(Model.Image))" />
                }
                else
                {
                    <img id="myImage" class="myProductImage" src="" />

                }

                <!--willing to accept only these type of images-->
                <!--onchange use single quotes -> can write server side quote, html works with both "" and '' -->
                <input asp-for="ImageFile" class="form-control" accept=".png, .jpg, .jpeg, .gif"
                       onchange='document.getElementById("myImage").src = window.URL.createObjectURL(this.files[0]);' />
                <!-- access the first file of file collection   , create object from url -->
                <span asp-validation-for="ImageFile" class="text-danger"></span>
            </div>