extends Node

var power_grid: int # Number of power stations in the area. Helps provide redundancies.
var power_capacity: int
var current_power_cap: int
var prev_power_cap: int
var has_power: bool
