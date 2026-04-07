Auto-Rigging & Animation API

The Auto-Rigging & Animation API allows you to programmatically rig 3D models and apply animations to them using Meshy's backend capabilities. This section provides the necessary details to integrate these features into your application.

Please note that programmatic rigging and animation currently only works well with standard humanoid (bipedal) assets with clearly defined limbs and body structure at this time.

Auto-Rigging API
Endpoints for submitting and retrieving character rigging tasks. Rigging involves creating an internal skeleton (armature) and binding the model's mesh (skin) to it, making it ready for animation.

POST
/openapi/v1/rigging
Create a Rigging Task
This endpoint allows you to create a new rigging task for a given 3D model. Upon successful completion, it provides a rigged character in standard formats and optionally basic walking/running animations.

Currently, auto-rigging is not suitable for the following models:

Untextured meshes
Non-humanoid assets
Humanoid assets with unclear limb and body structure
When using input_task_id, models with more than 300,000 faces are not supported for rigging. Please use the Remesh API to reduce the face count before rigging.

Parameters
Required attributes
Name
input_task_id
Type
string
Description
The input task that needs to be rigged. Required if model_url is not provided. We currently support textured humanoid models.

Only one of input_task_id or model_url may be used as input for rigging.

Name
model_url
Type
string
Description
Please provide a 3D model for Meshy to rig via a publicly accessible URL or Data URI. We currently support textured humanoid GLB files (.glb format). Required if input_task_id is not provided.

Only one of input_task_id or model_url may be used as input for rigging. If both are supplied, then the input_task_id will be used by default.

Optional attributes
Name
height_meters
Type
number
Description
The approximate height of the character model in meters. This aids in scaling and rigging accuracy. It must be a positive number. Default: 1.7.

Name
texture_image_url
Type
string
Description
The model's UV-unwrapped base color texture image. Publicly accessible URL or Data URI. We currently support .png formats.

Returns
The result property of the response contains the task id of the newly created rigging task.

Failure Modes
Name
400 - Bad Request
Description
The request was unacceptable. Common causes:

Missing parameter: Either model_url or input_task_id must be provided.
Invalid model format: The model_url points to a file with an unsupported extension (only .glb supported).
Unreachable URL: The model_url could not be downloaded.
Invalid input task: The input_task_id does not refer to a valid API task.
Face count exceeded: The input model has more than 300,000 faces. Please use the Remesh API to reduce the face count before rigging.
Name
401 - Unauthorized
Description
Authentication failed. Please check your API key.

Name
402 - Payment Required
Description
Insufficient credits to perform this task.

Name
422 - Unprocessable Entity
Description
Pose estimation failed. The provided model may not be a valid humanoid character.

Name
429 - Too Many Requests
Description
You have exceeded your rate limit.

Request
cURL
JavaScript
Python
POST
/openapi/v1/rigging
curl [https://api.meshy.ai/openapi/v1/rigging](https://api.meshy.ai/openapi/v1/rigging "https://api.meshy.ai/openapi/v1/rigging")
-X POST
-H "Authorization: Bearer ${YOUR_API_KEY}"
-H 'Content-Type: application/json'
-d '{
"model_url": "YOUR_MODEL_URL_OR_DATA_URI",
"height_meters": 1.8
}'

Copy
Copied!
Response
{
"result": "018b314a-a1b5-716d-c222-2f1776f7f579"
}

Copy
Copied!
GET
/openapi/v1/rigging/:id
Retrieve a Rigging Task
This endpoint allows you to retrieve a rigging task given a valid task id. Refer to The Rigging Task Object to see which properties are included.

Parameters
Name
id
Type
path
Description
Unique identifier for the rigging task to retrieve.

Returns
The response contains the Rigging Task object. Check The Rigging Task Object section for details.

Request
cURL
JavaScript
Python
GET
/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579
curl [https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579](https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579 "https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
{
"id": "018b314a-a1b5-716d-c222-2f1776f7f579",
"type": "rig",
"status": "SUCCEEDED",
"created_at": 1747032400453,
"progress": 100,
"started_at": 1747032401314,
"finished_at": 1747032418417,
"expires_at": 1747291618417,
"task_error": {
"message": ""
},
"result": {
"rigged_character_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.fbx?Expires=...&quot;");,
"rigged_character_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.glb?Expires=...&quot;");,
"basic_animations": {
"walking_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.glb?Expires=...&quot;");,
"walking_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.fbx?Expires=...&quot;");,
"walking_armature_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin_armature.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin_armature.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin_armature.glb?Expires=...&quot;");,
"running_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.glb?Expires=...&quot;");,
"running_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.fbx?Expires=...&quot;");,
"running_armature_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin_armature.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin_armature.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin_armature.glb?Expires=...&quot;");
}
},
"preceding_tasks": 0
}

Copy
Copied!
DELETE
/openapi/v1/rigging/:id
Delete a Rigging Task
This endpoint permanently deletes a rigging task, including all associated models and data. This action is irreversible.

Path Parameters
Name
id
Type
path
Description
The ID of the rigging task to delete.

Returns
Returns 200 OK on success.

Request
cURL
JavaScript
Python
DELETE
/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579
curl --request DELETE
--url [https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579](https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579 "https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
// Returns 200 Ok on success.

Copy
Copied!
GET
/openapi/v1/rigging/:id/stream
Stream a Rigging Task
This endpoint streams real-time updates for a Rigging task using Server-Sent Events (SSE).

Parameters
Name
id
Type
path
Description
Unique identifier for the Rigging task to stream.

Returns
Returns a stream of The Rigging Task Objects as Server-Sent Events.

For PENDING or IN_PROGRESS tasks, the response stream will only include necessary progress and status fields.

Request
cURL
JavaScript
Python
GET
/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579/stream
curl -N [https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579/stream](https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579/stream "https://api.meshy.ai/openapi/v1/rigging/018b314a-a1b5-716d-c222-2f1776f7f579/stream")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response Stream
// Error event example
event: error
data: {
"status_code": 404,
"message": "Task not found"
}

// Message event examples illustrate task progress.
// For PENDING or IN_PROGRESS tasks, the response stream will not include all fields.
event: message
data: {
"id": "018b314a-a1b5-716d-c222-2f1776f7f579",
"progress": 0,
"status": "PENDING"
}

event: message
data: {
"id": "018b314a-a1b5-716d-c222-2f1776f7f579",
"progress": 50,
"status": "IN_PROGRESS"
}

event: message
data: { // Example of a SUCCEEDED task stream item, mirroring The Rigging Task Object structure
"id": "018b314a-a1b5-716d-c222-2f1776f7f579",
"type": "rig",
"status": "SUCCEEDED",
"created_at": 1747032400453,
"progress": 100,
"started_at": 1747032401314,
"finished_at": 1747032418417,
"expires_at": 1747291618417,
"task_error": {
"message": ""
},
"result": {
"rigged_character_fbx_url": "[https://assets.meshy.ai/.../Character_output.fbx?...&#34;](https://assets.meshy.ai/.../Character_output.fbx?...%22 "https://assets.meshy.ai/.../Character_output.fbx?...&quot;");,
"rigged_character_glb_url": "[https://assets.meshy.ai/.../Character_output.glb?...&#34;](https://assets.meshy.ai/.../Character_output.glb?...%22 "https://assets.meshy.ai/.../Character_output.glb?...&quot;");,
"basic_animations": {
"walking_glb_url": "[https://assets.meshy.ai/.../Animation_Walking_withSkin.glb?...&#34;](https://assets.meshy.ai/.../Animation_Walking_withSkin.glb?...%22 "https://assets.meshy.ai/.../Animation_Walking_withSkin.glb?...&quot;");,
"walking_fbx_url": "[https://assets.meshy.ai/.../Animation_Walking_withSkin.fbx?...&#34;](https://assets.meshy.ai/.../Animation_Walking_withSkin.fbx?...%22 "https://assets.meshy.ai/.../Animation_Walking_withSkin.fbx?...&quot;");,
"walking_armature_glb_url": "[https://assets.meshy.ai/.../Animation_Walking_withSkin_armature.glb?...&#34;](https://assets.meshy.ai/.../Animation_Walking_withSkin_armature.glb?...%22 "https://assets.meshy.ai/.../Animation_Walking_withSkin_armature.glb?...&quot;");,
"running_glb_url": "[https://assets.meshy.ai/.../Animation_Running_withSkin.glb?...&#34;](https://assets.meshy.ai/.../Animation_Running_withSkin.glb?...%22 "https://assets.meshy.ai/.../Animation_Running_withSkin.glb?...&quot;");,
"running_fbx_url": "[https://assets.meshy.ai/.../Animation_Running_withSkin.fbx?...&#34;](https://assets.meshy.ai/.../Animation_Running_withSkin.fbx?...%22 "https://assets.meshy.ai/.../Animation_Running_withSkin.fbx?...&quot;");,
"running_armature_glb_url": "[https://assets.meshy.ai/.../Animation_Running_withSkin_armature.glb?...&#34;](https://assets.meshy.ai/.../Animation_Running_withSkin_armature.glb?...%22 "https://assets.meshy.ai/.../Animation_Running_withSkin_armature.glb?...&quot;");
}
},
"preceding_tasks": 0
}

Copy
Copied!
The Rigging Task Object
The Rigging Task object represents the work unit for rigging a character.

Properties
Name
id
Type
string
Description
Unique identifier for the task.

Name
type
Type
string
Description
Type of the Rigging task. The value is rig.

Name
status
Type
string
Description
Status of the task. Possible values: PENDING, IN_PROGRESS, SUCCEEDED, FAILED, CANCELED.

Name
progress
Type
integer
Description
Progress of the task (0-100). 0 if not started, 100 if succeeded.

Name
created_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task was created.

A timestamp represents the number of milliseconds elapsed since January 1, 1970 UTC, following the RFC 3339 standard. For example, Friday, September 1, 2023 12:00:00 PM GMT is represented as 1693569600000. This applies to all timestamps in Meshy API.

Name
started_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task started processing. 0 if not started.

Name
finished_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task finished. 0 if not finished.

Name
expires_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task result assets expire and may be deleted.

Name
task_error
Type
object
Description
Error object if the task failed, otherwise an object with an empty message string.

Name
message
Type
string
Description
Detailed error message. Empty if task succeeded.
Name
result
Type
object
Description
Contains the output asset URLs if the task SUCCEEDED, null otherwise.

Name
rigged_character_fbx_url
Type
string
Description
Downloadable URL for the rigged character in FBX format.

Name
rigged_character_glb_url
Type
string
Description
Downloadable URL for the rigged character in GLB format.

Name
basic_animations
Type
object (optional)
Description
Contains URLs for default animations. (e.g. if generate_basic_animations was implicitly true or enabled by default).

Name
walking_glb_url
Type
string
Description
Downloadable URL for walking animation in GLB format (with skin).
Name
walking_fbx_url
Type
string
Description
Downloadable URL for walking animation in FBX format (with skin).
Name
walking_armature_glb_url
Type
string
Description
Downloadable URL for walking animation armature in GLB format.
Name
running_glb_url
Type
string
Description
Downloadable URL for running animation in GLB format (with skin).
Name
running_fbx_url
Type
string
Description
Downloadable URL for running animation in FBX format (with skin).
Name
running_armature_glb_url
Type
string
Description
Downloadable URL for running animation armature in GLB format.
Name
preceding_tasks
Type
integer
Description
The count of preceding tasks in the queue. Meaningful only if status is PENDING.

Example Rigging Task Object
{
"id": "018b314a-a1b5-716d-c222-2f1776f7f579",
"type": "rig",
"status": "SUCCEEDED",
"created_at": 1747032400453,
"progress": 100,
"started_at": 1747032401314,
"finished_at": 1747032418417,
"expires_at": 1747291618417,
"task_error": {
"message": ""
},
"result": {
"rigged_character_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.fbx?Expires=...&quot;");,
"rigged_character_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Character_output.glb?Expires=...&quot;");,
"basic_animations": {
"walking_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.glb?Expires=...&quot;");,
"walking_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin.fbx?Expires=...&quot;");,
"walking_armature_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin_armature.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin_armature.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Walking_withSkin_armature.glb?Expires=...&quot;");,
"running_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.glb?Expires=...&quot;");,
"running_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin.fbx?Expires=...&quot;");,
"running_armature_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin_armature.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin_armature.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018b314a-a1b5-716d-c222-2f1776f7f579/output/Animation_Running_withSkin_armature.glb?Expires=...&quot;");
}
},
"preceding_tasks": 0
}

Copy
Copied!
Animation API
Endpoints for discovering available animations and applying them to rigged characters.

POST
/openapi/v1/animations
Create an Animation Task
This endpoint allows you to create a new task to apply a specific animation action to a previously rigged character. Includes post-processing options.

Parameters
Required attributes
Name
rig_task_id
Type
string
Description
The id of a successfully completed rigging task (from POST /openapi/v1/rigging). The character from this task will be animated.

Name
action_id
Type
integer
Description
The identifier of the animation action to apply. See the Animation Library Reference for a complete list of available animations.

Optional attributes
Name
post_process
Type
object
Description
Parameters for post-processing animation files.

Name
operation_type
Type
string (required)
Description
The type of operation to perform. Must be one of:

change_fps (adjusts frame rate)
fbx2usdz (converts FBX to USDZ)
extract_armature (extracts armature)
Name
fps
Type
integer (optional)
Description
The target frame rate. Default: 30. Applicable only when operation_type is change_fps. Allowed values: 24, 25, 30, 60.

Returns
The result property of the response contains the task id of the newly created animation task.

Failure Modes
Name
400 - Bad Request
Description
The request was unacceptable. Common causes:

Missing parameter: rig_task_id or action_id is missing.
Invalid rig task: The rig_task_id is invalid or refers to a failed/non-existent task.
Invalid action ID: The action_id does not correspond to a valid animation.
Name
401 - Unauthorized
Description
Authentication failed. Please check your API key.

Name
402 - Payment Required
Description
Insufficient credits to perform this task.

Name
404 - Not Found
Description
The rigging task specified by rig_task_id was not found.

Name
429 - Too Many Requests
Description
You have exceeded your rate limit.

Request
cURL
JavaScript
Python
POST
/openapi/v1/animations
curl [https://api.meshy.ai/openapi/v1/animations](https://api.meshy.ai/openapi/v1/animations "https://api.meshy.ai/openapi/v1/animations")
-X POST
-H "Authorization: Bearer ${YOUR_API_KEY}"
-H 'Content-Type: application/json'
-d '{
"rig_task_id": "018b314a-a1b5-716d-c222-2f1776f7f579",
"action_id": 92,
"post_process": {
"operation_type": "change_fps",
"fps": 60
}
}'

Copy
Copied!
Response
{
"result": "018c425b-b2c6-727e-d333-3c1887i9h791"
}

Copy
Copied!
GET
/openapi/v1/animations/:id
Retrieve an Animation Task
This endpoint allows you to retrieve an animation task given a valid task id. Refer to The Animation Task Object to see which properties are included.

Parameters
Name
id
Type
path
Description
Unique identifier for the animation task to retrieve.

Returns
The response contains the Animation Task object. Check The Animation Task Object section for details.

Request
cURL
JavaScript
Python
GET
/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791
curl [https://api.meshy.ai/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791](https://api.meshy.ai/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791 "https://api.meshy.ai/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
{
"id": "018c425b-b2c6-727e-d333-3c1887i9h791",
"type": "animate",
"status": "SUCCEEDED",
"created_at": 1747032440896,
"progress": 100,
"started_at": 1747032441210,
"finished_at": 1747032457530,
"expires_at": 1747291657530,
"task_error": {
"message": ""
},
"result": {
"animation_glb_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/Animation_Reaping_Swing_withSkin.glb?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/Animation_Reaping_Swing_withSkin.glb?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/Animation_Reaping_Swing_withSkin.glb?Expires=...&quot;");,
"animation_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/Animation_Reaping_Swing_withSkin.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/Animation_Reaping_Swing_withSkin.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/Animation_Reaping_Swing_withSkin.fbx?Expires=...&quot;");,
"processed_usdz_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed.usdz?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed.usdz?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed.usdz?Expires=...&quot;");,
"processed_armature_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed_armature.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed_armature.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed_armature.fbx?Expires=...&quot;");,
"processed_animation_fps_fbx_url": "[https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed_60fps.fbx?Expires=...&#34;](https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed_60fps.fbx?Expires=...%22 "https://assets.meshy.ai/0630d47c-84b8-4d37-bc02-69e45d9272c1/tasks/018c425b-b2c6-727e-d333-3c1887i9h791/output/processed_60fps.fbx?Expires=...&quot;");
},
"preceding_tasks": 0
}

Copy
Copied!
DELETE
/openapi/v1/animations/:id
Delete an Animation Task
This endpoint permanently deletes an animation task, including all associated models and data. This action is irreversible.

Path Parameters
Name
id
Type
path
Description
The ID of the animation task to delete.

Returns
Returns 200 OK on success.

Request
cURL
JavaScript
Python
DELETE
/openapi/v1/animations/018b314a-a1b5-716d-c222-2f1776f7f579
curl --request DELETE
--url [https://api.meshy.ai/openapi/v1/animations/018b314a-a1b5-716d-c222-2f1776f7f579](https://api.meshy.ai/openapi/v1/animations/018b314a-a1b5-716d-c222-2f1776f7f579 "https://api.meshy.ai/openapi/v1/animations/018b314a-a1b5-716d-c222-2f1776f7f579")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
// Returns 200 Ok on success.

Copy
Copied!
GET
/openapi/v1/animations/:id/stream
Stream an Animation Task
This endpoint streams real-time updates for an Animation task using Server-Sent Events (SSE).

Parameters
Name
id
Type
path
Description
Unique identifier for the Animation task to stream.

Returns
Returns a stream of The Animation Task Objects as Server-Sent Events.

For PENDING or IN_PROGRESS tasks, the response stream will only include necessary progress and status fields.

Request
cURL
JavaScript
Python
GET
/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791/stream
curl -N [https://api.meshy.ai/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791/stream](https://api.meshy.ai/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791/stream "https://api.meshy.ai/openapi/v1/animations/018c425b-b2c6-727e-d333-3c1887i9h791/stream")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response Stream
// Error event example
event: error
data: {
"status_code": 404,
"message": "Task not found"
}

// Message event examples illustrate task progress.
// For PENDING or IN_PROGRESS tasks, the response stream will not include all fields.
event: message
data: {
"id": "018c425b-b2c6-727e-d333-3c1887i9h791",
"progress": 0,
"status": "PENDING"
}

event: message
data: {
"id": "018c425b-b2c6-727e-d333-3c1887i9h791",
"progress": 50,
"status": "IN_PROGRESS"
}

event: message
data: { // Example of a SUCCEEDED task stream item, mirroring The Animation Task Object structure
"id": "018c425b-b2c6-727e-d333-3c1887i9h791",
"type": "animate",
"status": "SUCCEEDED",
"created_at": 1747032440896,
"progress": 100,
"started_at": 1747032441210,
"finished_at": 1747032457530,
"expires_at": 1747291657530,
"task_error": {
"message": ""
},
"result": {
"animation_glb_url": "[https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.glb?...&#34;](https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.glb?...%22 "https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.glb?...&quot;");,
"animation_fbx_url": "[https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.fbx?...&#34;](https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.fbx?...%22 "https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.fbx?...&quot;");,
"processed_usdz_url": "[https://assets.meshy.ai/.../processed.usdz?...&#34;](https://assets.meshy.ai/.../processed.usdz?...%22 "https://assets.meshy.ai/.../processed.usdz?...&quot;");,
"processed_armature_fbx_url": "[https://assets.meshy.ai/.../processed_armature.fbx?...&#34;](https://assets.meshy.ai/.../processed_armature.fbx?...%22 "https://assets.meshy.ai/.../processed_armature.fbx?...&quot;");,
"processed_animation_fps_fbx_url": "[https://assets.meshy.ai/.../processed_60fps.fbx?...&#34;](https://assets.meshy.ai/.../processed_60fps.fbx?...%22 "https://assets.meshy.ai/.../processed_60fps.fbx?...&quot;");
},
"preceding_tasks": 0
}

Copy
Copied!
The Animation Task Object
The Animation Task object represents the work unit for applying an animation to a rigged character.

Properties
Name
id
Type
string
Description
Unique identifier for the task.

Name
type
Type
string
Description
Type of the Animation task. The value is animate.

Name
status
Type
string
Description
Status of the task. Possible values: PENDING, IN_PROGRESS, SUCCEEDED, FAILED, CANCELED.

Name
progress
Type
integer
Description
Progress of the task (0-100).

Name
created_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task was created.

A timestamp represents the number of milliseconds elapsed since January 1, 1970 UTC, following the RFC 3339 standard. For example, Friday, September 1, 2023 12:00:00 PM GMT is represented as 1693569600000. This applies to all timestamps in Meshy API.

Name
started_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task started processing. 0 if not started.

Name
finished_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task finished. 0 if not finished.

Name
expires_at
Type
timestamp
Description
Timestamp (milliseconds since epoch) when the task result assets expire.

Name
task_error
Type
object
Description
Error object if the task failed, otherwise an object with an empty message string.

Name
message
Type
string
Description
Detailed error message. Empty if task succeeded.
Name
result
Type
object
Description
Contains the output animation URLs if the task SUCCEEDED.

Name
animation_glb_url
Type
string
Description
Downloadable URL for the animation in GLB format.
Name
animation_fbx_url
Type
string
Description
Downloadable URL for the animation in FBX format.
Name
processed_usdz_url
Type
string
Description
Downloadable URL for the processed animation in USDZ format.
Name
processed_armature_fbx_url
Type
string
Description
Downloadable URL for the processed armature in FBX format.
Name
processed_animation_fps_fbx_url
Type
string
Description
Downloadable URL for the animation with changed FPS in FBX format (e.g., if change_fps operation was used).
Name
preceding_tasks
Type
integer
Description
The count of preceding tasks in the queue. Meaningful only if status is PENDING.

Example Animation Task Object
{
"id": "018c425b-b2c6-727e-d333-3c1887i9h791",
"type": "animate",
"status": "SUCCEEDED",
"created_at": 1747032440896,
"progress": 100,
"started_at": 1747032441210,
"finished_at": 1747032457530,
"expires_at": 1747291657530,
"task_error": {
"message": ""
},
"result": {
"animation_glb_url": "[https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.glb?...&#34;](https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.glb?...%22 "https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.glb?...&quot;");,
"animation_fbx_url": "[https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.fbx?...&#34;](https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.fbx?...%22 "https://assets.meshy.ai/.../Animation_Reaping_Swing_withSkin.fbx?...&quot;");,
"processed_usdz_url": "[https://assets.meshy.ai/.../processed.usdz?...&#34;](https://assets.meshy.ai/.../processed.usdz?...%22 "https://assets.meshy.ai/.../processed.usdz?...&quot;");,
"processed_armature_fbx_url": "[https://assets.meshy.ai/.../processed_armature.fbx?...&#34;](https://assets.meshy.ai/.../processed_armature.fbx?...%22 "https://assets.meshy.ai/.../processed_armature.fbx?...&quot;");,
"processed_animation_fps_fbx_url": "[https://assets.meshy.ai/.../processed_60fps.fbx?...&#34;](https://assets.meshy.ai/.../processed_60fps.fbx?...%22 "https://assets.meshy.ai/.../processed_60fps.fbx?...&quot;");
},
"preceding_tasks": 0
}
