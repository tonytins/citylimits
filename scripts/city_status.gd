extends HBoxContainer

onready var city_name = $NameDate/CityNameLbl
onready var budget = $PopBudget/BudgetCtr/BudgetLbl
onready var calendar = $NameDate/YearCtr/CalendarLbl

func _process(delta):
	city_name.text = SimData.city_name
	budget.text = str(SimData.budget)
	var day = "%02d" % SimTime.day
	var month = "%02d" % SimTime.month
	calendar.text = str(day) + "/" + str(month) + "/" + str(SimTime.year)
