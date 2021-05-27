extends WindowDialog

var dialogue_file = "res://dialog/policies/proposels.json"

var dialogue_keys = []
var dialogue_name = ""
var dialogue_text = ""
var policy_id

onready var ordinance_label = $OrdinanceLbl
onready var description_label = $DescPanel/DescriptionLbl

func _start_dialogue(policy):
	match policy:
		[SimData.Ordinances.ENERGY_CONSERVATION, 1]:
			policy_id = 1
			_launch_policy_window(1)
		[SimData.Ordinances.CLEAN_AIR_ACT, 3]:
			policy_id = 2
			_launch_policy_window(2)
		[SimData.Ordinances.TIRE_RECYCLE, 3]:
			policy_id = 3
			_launch_policy_window(3)

func _launch_policy_window(key):
	_index_dialogue()
	var message: String = dialogue_keys[key].text
	
	if "[name]" in message:
		message = message.replace("[name]", SimData.mayor_name)
	
	if "[city]" in message:
		message = message.replace("[city]", SimData.city_name)
	
	description_label.text = message
	window_title = dialogue_keys[key].name
	show()

func _index_dialogue():
	var dialogue = _load_dialogue()
	dialogue_keys.clear()
	for key in dialogue:
		dialogue_keys.append(dialogue[key])

func _load_dialogue():
	var file = File.new()
	if file.file_exists(dialogue_file):
		file.open(dialogue_file, file.READ)
		var dialogue = parse_json(file.get_as_text())
		return dialogue

func _ready():
	SimEvents.connect("policy_message", self, "_start_dialogue")

func _on_IgnoreBtn_pressed():
	policy_id = 0
	description_label.text = ""
	ordinance_label.text = ""
	hide()

func _on_EnectBtn_pressed():
	match policy_id:
		1: SimEvents.emit_signal("energy_saving")
		2: SimEvents.emit_signal("clean_air_act")

func _on_AnalysisBtn_pressed():
	match policy_id:
		1: SimEvents.emit_signal("policy_analysis", SimData.Advisors.CITY_PLANNER, policy_id)
		2: SimEvents.emit_signal("policy_analysis", SimData.Advisors.CITY_PLANNER, policy_id)
		3: SimEvents.emit_signal("policy_analysis", SimData.Advisors.CITY_PLANNER, policy_id)
