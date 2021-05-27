extends AcceptDialog

var dialogue_file
# "character" would be "name" but it's already used by the base class
var character
var rank
var avatar

var dialogue_keys = []
var dialogue_name = ""
var dialogue_text = ""

onready var avatar_texture = $Container/Advisor/Avatar
onready var rank_label = $Container/Advisor/RankLbl
onready var name_label = $Container/Advisor/NameLbl
onready var description_label = $Container/DescriptionLbl

enum JsonFile {
	ANNOUNCEMENTS,
	ANALYSIS
}

func _ready():
	SimEvents.connect("advisor_message", self, "_advisor_dialogue")
	SimEvents.connect("policy_analysis", self, "_analysis_dialouge")
	
func _init_advisor(file, advisor):
	match advisor:
		SimData.Advisors.CITY_PLANNER:
			match file:
				JsonFile.ANNOUNCEMENTS:
					dialogue_file = "res://dialog/advisors/cityplanner.json"
				JsonFile.ANALYSIS:
					dialogue_file = "res://dialog/policies/cityplanner_analysis.json"
					
			avatar_texture.texture = preload("res://sprites/avatars/cindy.png")
			name_label.text = "Cindy Diamond"
			rank_label.text = "City Planner"
		
		SimData.Advisors.FINANCIAL:
			match file:
				JsonFile.ANNOUNCEMENTS:
					dialogue_file = "res://dialog/advisors/finacial.json"
				JsonFile.ANALYSIS:
					dialogue_file = "res://dialog/policies/finacial_analysis.json"
					
			avatar_texture.texture = preload("res://sprites/avatars/kit.png")
			name_label.text = "Kit Welsh"
			rank_label.text = "Financial Advisor"
			
		SimData.Advisors.TRANSPORT:
			match file:
				JsonFile.ANNOUNCEMENTS:
					dialogue_file = "res://dialog/advisors/transport.json"
				JsonFile.ANALYSIS:
					dialogue_file = "res://dialog/policies/transport_analysis.json"
					
			avatar_texture.texture = preload("res://sprites/avatars/zc.png")
			name_label.text = "Zack Casey"
			rank_label.text = "Transportation Advisor"
	
func _launch_advisor_window(key):
	_index_dialogue()
	var message: String = dialogue_keys[key].text
	
	if "[name]" in message:
		message = message.replace("[name]", SimData.mayor_name)
	
	if "[city]" in message:
		message = message.replace("[city]", SimData.city_name)
	
	description_label.text = message
	window_title = dialogue_keys[key].name
	show()

func _advisor_dialogue(advisor, message):
	_init_advisor(JsonFile.ANNOUNCEMENTS, advisor)
	_launch_advisor_window(message)
	
func _analysis_dialouge(advisor, policy):
	_init_advisor(JsonFile.ANALYSIS, advisor)
	_launch_advisor_window(policy)

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
