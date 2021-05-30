extends WindowDialog

onready var res_slider = $TaxSlidersCtr/ResCtr/ResSlider
onready var com_slider = $TaxSlidersCtr/ComCtr/ComSlider
onready var ind_slider = $TaxSlidersCtr/IndCtr/IndSlider

onready var res_rate = $PrecentCtr/ResPctLbl
onready var com_rate = $PrecentCtr/ComPctLbl
onready var ind_rate = $PrecentCtr/IndPctLbl

onready var annual_income = $IncomeCtr/IcnomeLbl

func _ready():
	if ZoneData.res_tax >= 0:
		res_slider.value = ZoneData.res_tax
		res_rate.text = str(ZoneData.res_tax) + "%"
	
	if ZoneData.res_tax >= 0:
		com_slider.value = ZoneData.comm_tax
		com_rate.text = str(ZoneData.comm_tax) + "%"
		
	if ZoneData.indust_tax >= 0:
		ind_slider.value = ZoneData.indust_tax
		ind_rate.text = str(ZoneData.indust_tax) + "%"
		
func _process(delta):
	var total_income = int(ZoneData.res_income + ZoneData.comm_income + ZoneData.ind_income)
	
	if SimTime.prev_month < SimTime.month and total_income > 1:
		annual_income.text = str(total_income) + "/mo"

func _on_ResSlider_value_changed(value):
	ZoneData.res_tax = int(value)
	res_rate.text = str(value) + "%"

func _on_ComSlider_value_changed(value):
	ZoneData.comm_tax = int(value)
	com_rate.text = str(value) + "%"

func _on_IndSlider_value_changed(value):
	ZoneData.indust_tax = int(value)
	ind_rate.text = str(value) + "%"
