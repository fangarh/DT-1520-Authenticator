package ru.dt1520.security.authenticator.feature.deviceonboarding

import java.net.URI
import org.json.JSONException
import org.json.JSONObject

class DeviceOnboardingPayload private constructor(
    val activationPayload: String,
    val runtimeBaseUrl: String?,
    val totpProvisioningPayload: String? = null
) {
    val value: String
        get() = activationPayload

    companion object {
        private const val Prefix = "dac_"
        private const val MaxActivationPayloadLength = 256
        private const val MaxQrPayloadLength = 4096
        private const val DeviceEnvelopeVersion = 1
        private const val CombinedEnvelopeVersion = 2
        private const val MaxTotpProvisioningPayloadLength = 2048
        private val allowedSecretPattern = Regex("^[A-Za-z0-9_+/=-]+$")

        fun parse(rawValue: String?): DeviceOnboardingPayload {
            val value = rawValue?.trim()
                ?: throw IllegalArgumentException("Activation payload is required.")
            require(value.isNotBlank()) {
                "Activation payload is required."
            }
            require(value.length <= MaxQrPayloadLength) {
                "Activation payload is too long."
            }

            return if (value.startsWith(Prefix)) {
                DeviceOnboardingPayload(
                    activationPayload = parseActivationPayload(value),
                    runtimeBaseUrl = null,
                    totpProvisioningPayload = null
                )
            } else {
                parseEnvelope(value)
            }
        }

        private fun parseEnvelope(value: String): DeviceOnboardingPayload {
            val json = try {
                JSONObject(value)
            } catch (_: JSONException) {
                throw IllegalArgumentException("QR envelope has unsupported format.")
            }

            require(json.has("v")) {
                "QR envelope has unsupported format."
            }
            val version = json.opt("v")
            require(version is Number && version.toInt() in setOf(DeviceEnvelopeVersion, CombinedEnvelopeVersion)) {
                "QR envelope has unsupported format."
            }

            val activationPayload = requiredString(json, "activationPayload")
            val runtimeBaseUrl = requiredString(json, "runtimeBaseUrl")
            val totpProvisioningPayload = when (version.toInt()) {
                CombinedEnvelopeVersion -> parseTotpProvisioningPayload(
                    requiredString(json, "totpProvisioningPayload")
                )
                else -> null
            }

            return DeviceOnboardingPayload(
                activationPayload = parseActivationPayload(activationPayload),
                runtimeBaseUrl = validateRuntimeBaseUrl(runtimeBaseUrl),
                totpProvisioningPayload = totpProvisioningPayload
            )
        }

        private fun parseActivationPayload(value: String): String {
            require(value.length <= MaxActivationPayloadLength) {
                "Activation payload is too long."
            }
            require(value.startsWith(Prefix)) {
                "Activation payload has unsupported format."
            }

            val separatorIndex = value.indexOf('.', Prefix.length)
            require(separatorIndex > Prefix.length) {
                "Activation payload has unsupported format."
            }

            val idPart = value.substring(Prefix.length, separatorIndex)
            require(idPart.length == 32 && idPart.all(Char::isLetterOrDigit)) {
                "Activation payload has unsupported format."
            }

            val secretPart = value.substring(separatorIndex + 1)
            require(secretPart.isNotBlank() && allowedSecretPattern.matches(secretPart)) {
                "Activation payload has unsupported format."
            }

            return value
        }

        private fun parseTotpProvisioningPayload(value: String): String {
            require(value.length <= MaxTotpProvisioningPayloadLength) {
                "TOTP provisioning payload is too long."
            }
            require(value.startsWith("otpauth://totp/", ignoreCase = true)) {
                "TOTP provisioning payload has unsupported format."
            }

            return value
        }

        private fun validateRuntimeBaseUrl(value: String): String {
            val uri = try {
                URI(value)
            } catch (_: IllegalArgumentException) {
                throw IllegalArgumentException("Runtime URL has unsupported format.")
            }

            require(uri.scheme == "https") {
                "Runtime URL has unsupported format."
            }
            require(!uri.host.isNullOrBlank()) {
                "Runtime URL has unsupported format."
            }
            require(uri.userInfo == null) {
                "Runtime URL has unsupported format."
            }

            return value
        }

        private fun requiredString(json: JSONObject, name: String): String {
            require(json.has(name)) {
                "QR envelope has unsupported format."
            }
            val value = json.opt(name)
            require(value is String && value.isNotBlank()) {
                "QR envelope has unsupported format."
            }

            return value.trim()
        }
    }
}
