package ru.dt1520.security.authenticator.app.deviceruntime

import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.time.Instant
import java.util.UUID
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval

internal class HttpDeviceRuntimeTransport(
    baseUrl: String,
    private val connectionFactory: (URL) -> HttpURLConnection = { url ->
        url.openConnection() as HttpURLConnection
    }
) : DeviceRuntimeTransport {
    private val normalizedBaseUrl = baseUrl.trimEnd('/')

    override suspend fun activate(
        command: DeviceActivationCommand,
        integrationAccessToken: String
    ): ActivatedDeviceSession {
        val responseBody = executeJsonRequest(
            method = "POST",
            path = "/api/v1/devices/activate",
            authorization = "Bearer $integrationAccessToken",
            requestBody = JSONObject()
                .put("tenantId", command.tenantId.toString())
                .put("externalUserId", command.externalUserId)
                .put("platform", "android")
                .put("activationCode", command.activationCode)
                .put("installationId", command.installationId)
                .applyOptional("deviceName", command.deviceName)
                .applyOptional("pushToken", command.pushToken)
                .applyOptional("publicKey", command.publicKey)
                .toString()
        )

        return parseActivationResponse(responseBody)
    }

    override suspend fun refresh(refreshToken: String): DeviceTokenEnvelope {
        val responseBody = executeJsonRequest(
            method = "POST",
            path = "/api/v1/auth/device-tokens/refresh",
            requestBody = JSONObject()
                .put("refreshToken", refreshToken)
                .toString()
        )

        return parseTokenEnvelope(responseBody)
    }

    override suspend fun listPending(accessToken: String): List<PendingPushApproval> {
        val responseBody = executeJsonRequest(
            method = "GET",
            path = "/api/v1/devices/me/challenges/pending",
            authorization = accessToken
        )

        return parsePendingApprovals(responseBody)
    }

    override suspend fun approve(
        challengeId: UUID,
        deviceId: UUID,
        accessToken: String,
        biometricVerified: Boolean
    ) {
        executeJsonRequest(
            method = "POST",
            path = "/api/v1/challenges/$challengeId/approve",
            authorization = accessToken,
            requestBody = JSONObject()
                .put("deviceId", deviceId.toString())
                .put("biometricVerified", biometricVerified)
                .toString()
        )
    }

    override suspend fun deny(
        challengeId: UUID,
        deviceId: UUID,
        accessToken: String,
        reason: String?
    ) {
        executeJsonRequest(
            method = "POST",
            path = "/api/v1/challenges/$challengeId/deny",
            authorization = accessToken,
            requestBody = JSONObject()
                .put("deviceId", deviceId.toString())
                .applyOptional("reason", reason)
                .toString()
        )
    }

    private fun executeJsonRequest(
        method: String,
        path: String,
        authorization: String? = null,
        requestBody: String? = null
    ): String {
        val connection = connectionFactory(URL("$normalizedBaseUrl$path"))

        try {
            connection.requestMethod = method
            connection.connectTimeout = CONNECT_TIMEOUT_MILLIS
            connection.readTimeout = READ_TIMEOUT_MILLIS
            connection.setRequestProperty("Accept", "application/json")
            authorization?.let { connection.setRequestProperty("Authorization", it) }

            if (requestBody != null) {
                connection.doOutput = true
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                connection.outputStream.use { output ->
                    output.write(requestBody.toByteArray(Charsets.UTF_8))
                }
            }

            val statusCode = connection.responseCode
            val responseBody = connection.readResponseBody()
            if (statusCode in 200..299) {
                return responseBody
            }

            throw createTransportException(statusCode, responseBody)
        } catch (exception: DeviceRuntimeTransportException) {
            throw exception
        } catch (exception: IOException) {
            throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.Network,
                message = "Device runtime request could not reach the backend.",
                cause = exception
            )
        } finally {
            connection.disconnect()
        }
    }

    private fun parseActivationResponse(responseBody: String): ActivatedDeviceSession {
        val json = parseJsonObject(responseBody)
        val deviceJson = json.requireObject("device")
        val tokensJson = json.requireObject("tokens")

        return ActivatedDeviceSession(
            deviceId = deviceJson.requireUuid("id"),
            tokens = parseTokenEnvelope(tokensJson.toString())
        )
    }

    private fun parseTokenEnvelope(responseBody: String): DeviceTokenEnvelope {
        val json = parseJsonObject(responseBody)

        return DeviceTokenEnvelope(
            accessToken = json.requireString("accessToken"),
            refreshToken = json.requireString("refreshToken"),
            tokenType = json.requireString("tokenType"),
            expiresInSeconds = json.requireInt("expiresIn"),
            scope = json.requireString("scope")
        )
    }

    private fun parsePendingApprovals(responseBody: String): List<PendingPushApproval> {
        val array = try {
            JSONArray(responseBody)
        } catch (exception: JSONException) {
            throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Pending push approvals response is invalid.",
                cause = exception
            )
        }

        return buildList(array.length()) {
            for (index in 0 until array.length()) {
                val item = array.optJSONObject(index)
                    ?: throw DeviceRuntimeTransportException(
                        kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                        message = "Pending push approvals response item is invalid."
                    )

                add(
                    PendingPushApproval(
                        id = item.requireUuid("id"),
                        operationType = item.requireString("operationType"),
                        operationDisplayName = item.optionalTrimmedString("operationDisplayName"),
                        username = item.optionalTrimmedString("username"),
                        expiresAt = item.requireInstant("expiresAt"),
                        correlationId = item.optionalTrimmedString("correlationId")
                    )
                )
            }
        }
    }

    private fun createTransportException(
        statusCode: Int,
        responseBody: String
    ): DeviceRuntimeTransportException {
        val problem = parseProblem(responseBody)
        val message = problem?.detail
            ?: problem?.title
            ?: "Device runtime request failed with HTTP $statusCode."

        return DeviceRuntimeTransportException(
            kind = when (statusCode) {
                HttpURLConnection.HTTP_UNAUTHORIZED -> DeviceRuntimeTransportFailureKind.Unauthorized
                HttpURLConnection.HTTP_FORBIDDEN -> DeviceRuntimeTransportFailureKind.Forbidden
                HttpURLConnection.HTTP_NOT_FOUND -> DeviceRuntimeTransportFailureKind.NotFound
                HttpURLConnection.HTTP_CONFLICT -> DeviceRuntimeTransportFailureKind.Conflict
                HTTP_GONE -> DeviceRuntimeTransportFailureKind.Gone
                HttpURLConnection.HTTP_BAD_REQUEST,
                HTTP_UNPROCESSABLE_ENTITY -> DeviceRuntimeTransportFailureKind.Validation
                in 500..599 -> DeviceRuntimeTransportFailureKind.Server
                else -> DeviceRuntimeTransportFailureKind.Network
            },
            message = message
        )
    }

    private fun parseProblem(responseBody: String): ProblemPayload? {
        if (responseBody.isBlank()) {
            return null
        }

        return try {
            val json = JSONObject(responseBody)
            ProblemPayload(
                title = json.optionalTrimmedString("title"),
                detail = json.optionalTrimmedString("detail")
            )
        } catch (_: JSONException) {
            null
        }
    }

    private fun parseJsonObject(responseBody: String): JSONObject {
        return try {
            JSONObject(responseBody)
        } catch (exception: JSONException) {
            throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Device runtime response body is invalid.",
                cause = exception
            )
        }
    }

    private fun JSONObject.requireObject(name: String): JSONObject {
        val value = optJSONObject(name)
            ?: throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Device runtime response field '$name' is missing."
            )

        return value
    }

    private fun JSONObject.requireString(name: String): String {
        val value = optionalTrimmedString(name)
            ?: throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Device runtime response field '$name' is missing."
            )

        return value
    }

    private fun JSONObject.requireInt(name: String): Int {
        return if (has(name)) {
            optInt(name, Int.MIN_VALUE)
        } else {
            Int.MIN_VALUE
        }.takeIf { it > 0 }
            ?: throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Device runtime response field '$name' is invalid."
            )
    }

    private fun JSONObject.requireUuid(name: String): UUID {
        return try {
            UUID.fromString(requireString(name))
        } catch (exception: IllegalArgumentException) {
            throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Device runtime response field '$name' is not a valid UUID.",
                cause = exception
            )
        }
    }

    private fun JSONObject.requireInstant(name: String): Instant {
        return try {
            Instant.parse(requireString(name))
        } catch (exception: Exception) {
            throw DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.InvalidResponse,
                message = "Device runtime response field '$name' is not a valid instant.",
                cause = exception
            )
        }
    }

    private fun JSONObject.optionalTrimmedString(name: String): String? {
        if (isNull(name) || !has(name)) {
            return null
        }

        return optString(name)
            .trim()
            .takeIf(String::isNotBlank)
    }

    private fun JSONObject.applyOptional(
        name: String,
        value: String?
    ): JSONObject {
        value?.takeIf(String::isNotBlank)?.let { put(name, it) }
        return this
    }

    private fun HttpURLConnection.readResponseBody(): String {
        val stream = errorStream ?: runCatching { inputStream }.getOrNull() ?: return ""
        return stream.bufferedReader(Charsets.UTF_8).use { reader ->
            reader.readText()
        }
    }

    private data class ProblemPayload(
        val title: String?,
        val detail: String?
    )

    private companion object {
        const val CONNECT_TIMEOUT_MILLIS: Int = 10_000
        const val READ_TIMEOUT_MILLIS: Int = 10_000
        const val HTTP_GONE: Int = 410
        const val HTTP_UNPROCESSABLE_ENTITY: Int = 422
    }
}
