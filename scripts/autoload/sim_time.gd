extends Node

var year: int = 2000 setget increment_year
var prev_year: int
var month: int = 1 setget increment_month
var prev_month: int 
var day: int = 1 setget increment_day
var prev_day: int

func new_year():
	prev_year = year
	prev_day = day
	prev_month = month
	increment_year(1)
	day = 1
	month = 1
	
func reset_day():
	prev_day = day
	day = 1

func increment_day(new_day):
	prev_day = day
	day += new_day

func increment_year(new_year):
	prev_year = year
	year += new_year
	
func increment_month(new_month):
	prev_month = month
	month += new_month
