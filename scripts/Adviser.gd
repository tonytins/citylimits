extends AcceptDialog

export(String, FILE, "*.json") var dialogue_file
export var advisor_name: String
export var rank: String
export(Texture) var avatar
export var dialog = 0

var dialogue_keys = []
var dialogue_name = ""
var dialogue_text = ""

onready var avatar_texture = $Container/Advisor/Avatar
onready var rank_label = $Container/Advisor/RankLbl
onready var name_label = $Container/Advisor/NameLbl
onready var description_label = $Container/DescriptionLbl

func start_dialogue():
	index_dialogue()
	description_label.text = dialogue_keys[dialog].text
	self.window_title = dialogue_keys[dialog].name
	

func index_dialogue():
	var dialogue = load_dialogue(dialogue_file)
	dialogue_keys.clear()
	for key in dialogue:
		dialogue_keys.append(dialogue[key])

func load_dialogue(file_path):
	var file = File.new()
	if file.file_exists(file_path):
		file.open(file_path, file.READ)
		var dialogue = parse_json(file.get_as_text())
		return dialogue

func _ready():
	load_dialogue(dialogue_file)
	self.show()
	start_dialogue()
	avatar_texture.texture = avatar
	name_label.text = advisor_name
	rank_label.text = rank
