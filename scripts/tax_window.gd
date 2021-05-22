extends WindowDialog

onready var res_slider = $TaxSlidersCtr/ResCtr/ResSlider
onready var com_slider = $TaxSlidersCtr/ComCtr/ComSlider
onready var ind_slider = $TaxSlidersCtr/IndCtr/IndSlider

onready var res_rate = $PrecentCtr/ResPctLbl
onready var com_rate = $PrecentCtr/ComPctLbl
onready var ind_rate = $PrecentCtr/IndPctLbl

onready var annual_income = $IncomeCtr/IcnomeLbl

func _ready():
	if SimData.res_tax >= 0:
		res_slider.value = SimData.res_tax
		res_rate.text = str(SimData.res_tax) + "%"
	
	if SimData.res_tax >= 0:
		com_slider.value = SimData.comm_tax
		com_rate.text = str(SimData.comm_tax) + "%"
		
	if SimData.indust_tax >= 0:
		ind_slider.value = SimData.indust_tax
		ind_rate.text = str(SimData.indust_tax) + "%"
		
func _process(delta):
	var total_income = int(SimData.res_income + SimData.comm_income + SimData.ind_income)
	
	
	if total_income >= 1:
		annual_income.text = "Income: " + SimData.currency + str(total_income) + "/yr"

func _on_ResSlider_value_changed(value):
	SimData.res_tax = int(value)
	res_rate.text = str(value) + "%"

func _on_ComSlider_value_changed(value):
	SimData.comm_tax = int(value)
	com_rate.text = str(value) + "%"

func _on_IndSlider_value_changed(value):
	SimData.indust_tax = int(value)
	ind_rate.text = str(value) + "%"
