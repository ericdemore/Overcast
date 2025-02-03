# Overcast

Overcast is an MVC .NET application designed to help users find mutual actors between two movies or TV shows. The application leverages the TMDB (The Movie Database) API to fetch and compare cast data, presenting the results in an intuitive and user-friendly interface.

<img width="1194" alt="image" src="https://github.com/user-attachments/assets/10b9a896-6a3f-4fb8-8f0f-5fcc6ea10271" />


Check it out online at https://overcasted.azurewebsites.net

## Features
- **Mutual Cast Finder:** Compare any two movies or TV shows to identify actors who appeared in both.
- **Interactive UI:** Simple input fields for quick title searches.
- **Detailed Actor Information:** Displays actor names, roles, and links to their profiles.

## Technologies Used
- ASP.NET MVC
- C#
- TMDB API
- HTML/CSS
- JavaScript

## Prerequisites
- Visual Studio 2019 or later
- .NET 6 SDK
- TMDB API Key (Sign up at [TMDB](https://www.themoviedb.org/) to get your key)

## Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/ericdemore/Overcast
   ```
2. Open the solution file in Visual Studio.
3. Add your TMDB API key in `appsettings.json`:
   ```json
   {
     "TMDB": {
       "ApiKey": "YOUR_API_KEY"
     }
   }
   ```
4. Build and run the application.

## Usage
1. Enter the names of two movies or TV shows in the respective input fields.
2. Click the **Find Common Cast** button.
3. View the results, which display the common actors, their roles in each title, and additional details.


## Contributing
1. Fork the repository.
2. Create a new branch (`git checkout -b feature-branch`).
3. Make your changes and commit (`git commit -m 'Add new feature'`).
4. Push to the branch (`git push origin feature-branch`).
5. Open a pull request.

## License
This project is licensed under the MIT License.
