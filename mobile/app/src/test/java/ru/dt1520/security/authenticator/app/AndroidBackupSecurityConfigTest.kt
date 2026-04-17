package ru.dt1520.security.authenticator.app

import java.io.File
import javax.xml.parsers.DocumentBuilderFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import org.w3c.dom.Element

class AndroidBackupSecurityConfigTest {
    @Test
    fun manifestDisablesBackupAndKeepsExplicitBackupRuleReferences() {
        val application = loadDocument("src/main/AndroidManifest.xml")
            .documentElement
            .getElementsByTagName("application")
            .item(0) as Element

        assertFalse(
            application.getAttributeNS(ANDROID_NAMESPACE, "allowBackup").toBoolean()
        )
        assertEquals(
            "@xml/backup_rules",
            application.getAttributeNS(ANDROID_NAMESPACE, "fullBackupContent")
        )
        assertEquals(
            "@xml/data_extraction_rules",
            application.getAttributeNS(ANDROID_NAMESPACE, "dataExtractionRules")
        )
    }

    @Test
    fun legacyBackupRulesExcludeAllSupportedDomains() {
        val excludes = loadExcludeRules("src/main/res/xml/backup_rules.xml")

        assertEquals(EXPECTED_BACKUP_DOMAINS, excludes)
    }

    @Test
    fun dataExtractionRulesExcludeAllSupportedDomainsForCloudAndDeviceTransfer() {
        val document = loadDocument("src/main/res/xml/data_extraction_rules.xml")
        val root = document.documentElement

        assertEquals(
            EXPECTED_BACKUP_DOMAINS,
            childExcludeDomains(root, "cloud-backup")
        )
        assertEquals(
            EXPECTED_BACKUP_DOMAINS,
            childExcludeDomains(root, "device-transfer")
        )
    }

    private fun loadExcludeRules(relativePath: String): Set<String> =
        childExcludeDomains(loadDocument(relativePath).documentElement, "full-backup-content")

    private fun childExcludeDomains(root: Element, containerTagName: String): Set<String> {
        val container = if (root.tagName == containerTagName) {
            root
        } else {
            root.getElementsByTagName(containerTagName).item(0) as Element
        }

        val excludes = container.getElementsByTagName("exclude")

        return buildSet(excludes.length) {
            repeat(excludes.length) { index ->
                val element = excludes.item(index) as Element
                assertEquals(".", element.getAttribute("path"))
                add(element.getAttribute("domain"))
            }
        }
    }

    private fun loadDocument(relativePath: String) =
        DocumentBuilderFactory.newInstance()
            .apply {
                isNamespaceAware = true
            }
            .newDocumentBuilder()
            .parse(File(relativePath))

    private companion object {
        const val ANDROID_NAMESPACE: String = "http://schemas.android.com/apk/res/android"

        val EXPECTED_BACKUP_DOMAINS: Set<String> = setOf(
            "root",
            "file",
            "database",
            "sharedpref",
            "external",
            "device_root",
            "device_file",
            "device_database",
            "device_sharedpref"
        )
    }
}
