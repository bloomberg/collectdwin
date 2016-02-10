using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BloombergFLP.CollectdWin;

namespace BloombergFLP.CollectdWinTest
{
    [TestClass]
    public class MetricValueTests
    {
        private MetricValue val1, val2;
        private string val1Str, val2Str;


        public MetricValueTests()
        {
            initTest();
        }

        private void initTest()
        {
            val1 = new MetricValue
            {
                HostName = "host-1",
                PluginName = "cpu",
                PluginInstanceName = "",
                TypeName = "percent",
                TypeInstanceName = "",
                Values = new double[] { 123.0 }
            };
            val1Str = @"{""host"":""host-1"", ""plugin"":""cpu"", ""plugin_instance"":"""", ""type"":""percent"", " +
                @"""type_instance"":"""", ""time"":0, ""interval"":0, ""dstypes"":[""gauge""], ""dsnames"":[""value""], ""values"":[123]}";
            val2 = new MetricValue
            {
                HostName = "host-2",
                PluginName = "cpu",
                PluginInstanceName = "",
                TypeName = "percent",
                TypeInstanceName = "",
                Values = new double[] { 123.0 },                
            };
            val2.AddMetaData("region", "ny");
            val2.AddMetaData("dc", "datacenter-1");
            val2Str = @"{""host"":""host-2"", ""plugin"":""cpu"", ""plugin_instance"":"""", ""type"":""percent"", " +
                @"""type_instance"":"""", ""time"":0, ""interval"":0, ""dstypes"":[""gauge""], ""dsnames"":[""value""], ""values"":[123]" +
                @", ""meta"":{""dc"":""datacenter-1"",""region"":""ny""}}";
        }

        [TestMethod]
        public void MetricWithEmptyTags()
        {
            string expected = val1.GetMetricJsonStr();
            Assert.AreEqual(expected, val1Str, "MetricWithEmptyTags failed");
        }

        [TestMethod]
        public void MetricWithTwoTags()
        {
            string expected = val2.GetMetricJsonStr();
            Assert.AreEqual(expected, val2Str, "MetricWithTwoTags failed");
        }
    }
}