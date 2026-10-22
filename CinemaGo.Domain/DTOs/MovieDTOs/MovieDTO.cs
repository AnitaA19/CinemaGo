using System;
using System.Collections.Generic;
using System.Text;

namespace CinemaGo.Domain.DTOs.MovieDTOs
{
    public class MovieDTO
    {
        public string Title { get; set; }
        public int DurationMinutes { get; set; }
        public int AgeRating { get; set; }
    }
}
