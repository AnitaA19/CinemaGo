using System;
using System.Collections.Generic;
using System.Text;

namespace CinemaGo.Domain.DTOs.MovieDTOs
{
    public class MovieDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int DurationMinutes { get; set; }
        public int AgeRating { get; set; }
    }
}
