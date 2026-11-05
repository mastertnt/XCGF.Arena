using XCGF.Arena.Data;

namespace XCGF.Arena.Tests;

public class XcgfDataLoaderTests
{
    private const string Header = "<XcgfData xmlns=\"urn:xcgf:data:1.0\">";

    private const string BaseTypes = """
        <DataTypes>
          <BasicData name="F64" size="64" interpretation="Double-precision floating point" endian="Big" encoding="IEEE 754"/>
          <SimpleData name="Speed" representation="F64" units="m/s"/>
          <FixedRecordData name="GeoPosition">
            <Field name="Latitude" dataType="F64"/><Field name="Longitude" dataType="F64"/><Field name="Altitude" dataType="F64"/>
          </FixedRecordData>
          <FixedRecordData name="Orientation">
            <Field name="Heading" dataType="F64"/><Field name="Pitch" dataType="F64"/><Field name="Roll" dataType="F64"/>
          </FixedRecordData>
        </DataTypes>
        """;

    private const string RootKinematic = """
        <KinematicDefinition name="Kinematic">
          <InitializationData>
            <Parameter name="Position" dataType="GeoPosition"/>
            <Parameter name="Orientation" dataType="Orientation"/>
          </InitializationData>
        </KinematicDefinition>
        """;

    [Fact]
    public void Load_ExampleLibrary_ReadsEverySection()
    {
        ArchetypeLibrary lLibrary = TestLibrary.Example;

        Assert.Equal(50, lLibrary.DataTypes.Count);
        Assert.Equal(27, lLibrary.EquipmentDefinitions.Count);
        Assert.Superset(
            new HashSet<string> { "Kinematic", "FixedWingKinematic", "Sensor", "Radar", "ESM", "InfraredSensor", "VisualSensor", "Signature", "FighterRadar" },
            lLibrary.EquipmentDefinitions.Keys.ToHashSet());
        Assert.Equal(new[] { "ChangeFormation", "FireOnTarget", "FollowFlightPlan", "MoveTo" }, lLibrary.Tasks.Keys.Order());
        Assert.Equal(new[] { "F35A", "Fighter", "Rafale" }, lLibrary.Entities.Keys.Order());
        Assert.Equal(new[] { "FighterPatrol" }, lLibrary.Units.Keys);
        Assert.Empty(lLibrary.Validate());
    }

    [Fact]
    public void Load_ExampleLibrary_ReadsDataTypes()
    {
        ArchetypeLibrary lLibrary = TestLibrary.Example;

        BasicDataType lFloat = Assert.IsType<BasicDataType>(lLibrary.GetDataType("HLAfloat64BE"));
        Assert.True(lFloat.IsFloatingPoint);
        Assert.False(Assert.IsType<BasicDataType>(lLibrary.GetDataType("HLAinteger32BE")).IsFloatingPoint);
        Assert.Equal("m/s", Assert.IsType<SimpleDataType>(lLibrary.GetDataType("SpeedMetersPerSecond")).Units);
        Assert.Equal(5, Assert.IsType<EnumeratedDataType>(lLibrary.GetDataType("FormationEnum")).Enumerators.Count);
        Assert.Equal("1..100", Assert.IsType<ArrayDataType>(lLibrary.GetDataType("FlightPlan")).Cardinality);
        Assert.Equal(3, Assert.IsType<FixedRecordDataType>(lLibrary.GetDataType("GeoPosition")).Fields.Count);
        Assert.Equal(ReferenceTarget.Equipment, Assert.IsType<ReferenceDataType>(lLibrary.GetDataType("EquipmentReference")).Target);
    }

    [Fact]
    public void Load_ExampleLibrary_ReadsEntityAndUnit()
    {
        EntityArchetype lFighter = TestLibrary.Example.GetEntity("Fighter");
        UnitArchetype lPatrol = TestLibrary.Example.GetUnit("FighterPatrol");

        Assert.Equal("1.2.71.1.0.0.0", lFighter.DisType);
        Assert.Equal(EntityEnvironment.Air, lFighter.Environment);
        Assert.Equal("Aircraft/Fighter", lFighter.Folder);
        Assert.Equal("FighterKinematic", lFighter.Kinematic.Definition);
        Assert.Equal("250", Assert.Single(lFighter.Kinematic.InitializationData).DefaultValue!.Text);
        Assert.Equal(new[] { "Signature", "Radar", "RWR", "IRST", "Missile_1", "Missile_2" }, lFighter.Equipments.Select(pEquipment => pEquipment.Name));
        Assert.Equal(new[] { "MoveTo", "FireOnTarget", "FollowFlightPlan" }, lFighter.Tasks.Select(pTask => pTask.Task));
        Assert.Equal(new[] { "Leader", "Wingman" }, lPatrol.Members.Select(pMember => pMember.Role));
        Assert.All(lPatrol.Members, pMember => Assert.Equal(("Fighter", false, 1), (pMember.Archetype, pMember.IsUnit, pMember.Count)));
        Assert.Equal("Line", lPatrol.Tasks.Single(pTask => pTask.Task == "ChangeFormation").Parameters.Single().DefaultValue!.Text);
    }

    [Fact]
    public void Load_ExampleLibrary_ReadsTasks()
    {
        TaskDefinition lMoveTo = TestLibrary.Example.GetTask("MoveTo");
        TaskDefinition lFire = TestLibrary.Example.GetTask("FireOnTarget");
        TaskDefinition lPlan = TestLibrary.Example.GetTask("FollowFlightPlan");

        GroupedTaskParameter lDestination = lMoveTo.FindParameter("Destination")!;
        Assert.Equal(TaskParameterGroup.Where, lDestination.Group);
        Assert.True(lDestination.Parameter.Required);
        Assert.Equal(ParameterDirection.Out, lMoveTo.FindParameter("TravelTime")!.Parameter.Direction);
        Assert.IsType<ImmediateTrigger>(lMoveTo.ActivationTrigger);
        Assert.Equal(new OnPositionReachedTrigger("Destination", 500), lMoveTo.ExitTriggers[0].Trigger);
        Assert.Equal(new ExitTrigger("Failure", new AtTimeTrigger(TimeSpan.FromHours(1))), lMoveTo.ExitTriggers[1]);
        Assert.Equal(new OnConditionTrigger("InRange(Weapon, Target)"), lFire.ActivationTrigger);
        Assert.Equal(new[] { "Success", "Failure", "Aborted" }, lFire.ExitTriggers.Select(pExit => pExit.Outcome));
        Assert.Equal(TaskParameterGroup.WhoTasker, lFire.Parameters[0].Group);
        Assert.Equal(new OnTaskEndTrigger("MoveTo", "Success"), lPlan.ActivationTrigger);
        Assert.Equal(ParameterDirection.InOut, lPlan.FindParameter("CurrentWaypoint")!.Parameter.Direction);
    }

    [Fact]
    public void Load_ExampleLibrary_ReadsConstraints()
    {
        EquipmentDefinition lKinematic = TestLibrary.Example.EquipmentDefinitions["FixedWingKinematic"];
        EquipmentDefinition lSensor = TestLibrary.Example.EquipmentDefinitions["Sensor"];

        Assert.True(lKinematic.IsKinematic);
        Assert.Equal("Kinematic", lKinematic.Base);
        Assert.Equal(
            new DataConstraint[] { new BoundConstraint(BoundKind.Min, null, "StallSpeed", true), new BoundConstraint(BoundKind.Max, null, "MaxSpeed", true) },
            lKinematic.InitializationData.Single(pItem => pItem.Name == "InitialSpeed").Constraints);
        Assert.Equal(
            new DataConstraint[] { new BoundConstraint(BoundKind.Min, "0", null, true), new BoundConstraint(BoundKind.Max, null, "MaxRange", true) },
            lSensor.TechnologicalData.Single(pItem => pItem.Name == "MinRange").Constraints);
        Assert.Equal(new BoundConstraint(BoundKind.Min, "0", null, false), lSensor.TechnologicalData.Single(pItem => pItem.Name == "MaxRange").Constraints.Single());
        Assert.Equal("Radar", TestLibrary.Example.EquipmentDefinitions["FighterRadar"].Base);
    }

    [Theory]
    [InlineData("../XCGF.Data/library/types/core.xml")]
    [InlineData("../XCGF.Data/library/equipments/sensors.xml")]
    [InlineData("equipments.xml")]
    [InlineData("tasks.xml")]
    [InlineData("entities/fighter.xml")]
    [InlineData("units.xml")]
    public void LoadInto_EachExampleFile_LoadsOnItsOwn(string pFile)
    {
        ArchetypeLibrary lLibrary = new ArchetypeLibrary();

        XcgfDataLoader.LoadInto(lLibrary, TestLibrary.PathOf(pFile));

        int lCount = lLibrary.DataTypes.Count + lLibrary.EquipmentDefinitions.Count + lLibrary.Tasks.Count + lLibrary.Entities.Count + lLibrary.Units.Count;
        Assert.True(lCount > 0);
    }

    [Fact]
    public void Load_SeveralFiles_AssemblesThem()
    {
        string[] lFiles =
        {
            "../XCGF.Data/library/types/core.xml", "../XCGF.Data/library/types/equipment.xml",
            "../XCGF.Data/library/equipments/kinematics.xml", "../XCGF.Data/library/equipments/signatures.xml",
            "../XCGF.Data/library/equipments/sensors.xml", "equipments.xml", "tasks.xml", "entities/fighter.xml", "units.xml",
        };

        ArchetypeLibrary lLibrary = XcgfDataLoader.Load(lFiles.Select(TestLibrary.PathOf).ToArray());

        Assert.Single(lLibrary.Entities);
        Assert.Single(lLibrary.Units);
    }

    [Fact]
    public void Load_FileThatIncludesItself_Throws()
    {
        string lDirectory = Directory.CreateTempSubdirectory().FullName;
        string lPath = Path.Combine(lDirectory, "loop.xml");
        File.WriteAllText(lPath, Header + "<xi:include xmlns:xi=\"http://www.w3.org/2001/XInclude\" href=\"loop.xml\"/></XcgfData>");

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => XcgfDataLoader.Load(lPath));

        Assert.Contains("includes itself", lError.Message);
    }

    [Fact]
    public void Validate_ReportsUnknownReferences()
    {
        ArchetypeLibrary lLibrary = TestLibrary.FromXml(Header + BaseTypes + """
            <EquipmentLibrary>
            """ + RootKinematic + """
              <EquipmentDefinition name="Sensor" base="Missing">
                <TechnologicalData><Parameter name="Range" dataType="Unknown"/></TechnologicalData>
              </EquipmentDefinition>
            </EquipmentLibrary>
            <Entities>
              <Entity name="Car" disType="1.1.1.1.0.0.0">
                <Kinematic definition="Kinematic"/>
                <Tasks><TaskRef task="Drive"/></Tasks>
              </Entity>
            </Entities>
            <Units><Unit name="Convoy"><EntityRef entity="Truck"/></Unit></Units>
            </XcgfData>
            """);

        IReadOnlyList<string> lErrors = lLibrary.Validate();

        Assert.Contains(lErrors, pError => pError.Contains("unknown data type 'Unknown'"));
        Assert.Contains(lErrors, pError => pError.Contains("Unknown equipment definition 'Missing'"));
        Assert.Contains(lErrors, pError => pError.Contains("unknown task 'Drive'"));
        Assert.Contains(lErrors, pError => pError.Contains("unknown entity 'Truck'"));
    }

    [Fact]
    public void Validate_ReportsRulesXsdCannotExpress()
    {
        ArchetypeLibrary lLibrary = TestLibrary.FromXml(Header + BaseTypes + """
            <EquipmentLibrary>
            """ + RootKinematic + """
              <KinematicDefinition name="Orphan">
                <InitializationData>
                  <Parameter name="Speed" dataType="Speed"><Constraints><Max ref="MaxSpeed"/></Constraints></Parameter>
                </InitializationData>
              </KinematicDefinition>
              <EquipmentDefinition name="A" base="B"/>
              <EquipmentDefinition name="B" base="A"/>
            </EquipmentLibrary>
            <Entities>
              <Entity name="Drone" disType="1.2.1.1.0.0.0"><Kinematic definition="Orphan"/></Entity>
            </Entities>
            <Units>
              <Unit name="Loop1"><UnitRef unit="Loop2"/></Unit>
              <Unit name="Loop2"><UnitRef unit="Loop1"/></Unit>
            </Units>
            </XcgfData>
            """);

        IReadOnlyList<string> lErrors = lLibrary.Validate();

        Assert.Contains(lErrors, pError => pError.Contains("'Orphan' does not derive from 'Kinematic'"));
        Assert.Contains(lErrors, pError => pError.Contains("derives from itself"));
        Assert.Contains(lErrors, pError => pError.Contains("references unknown technological data 'MaxSpeed'"));
        Assert.Contains(lErrors, pError => pError.Contains("declares no 'Position'"));
        Assert.Contains(lErrors, pError => pError.Contains("Unit 'Loop1' contains itself"));
    }

    [Fact]
    public void Load_InconsistentLibrary_Throws()
    {
        string lDirectory = Directory.CreateTempSubdirectory().FullName;
        string lPath = Path.Combine(lDirectory, "bad.xml");
        File.WriteAllText(lPath, Header + "<Units><Unit name=\"U\"><EntityRef entity=\"Nobody\"/></Unit></Units></XcgfData>");

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => XcgfDataLoader.Load(lPath));

        Assert.Contains("unknown entity 'Nobody'", lError.Message);
    }

    [Fact]
    public void ParseValue_ConvertsAccordingToDataType()
    {
        ArchetypeLibrary lLibrary = TestLibrary.Example;

        Assert.Equal(new NumberValue(250), lLibrary.ParseValue("SpeedMetersPerSecond", "250"));
        Assert.Equal(new IntegerValue(2), lLibrary.ParseValue("Count", "2"));
        Assert.Equal(new EnumValue("Track", 2), lLibrary.ParseValue("RadarModeEnum", "Track"));
        Assert.Equal(new EnumValue("Track", 2), lLibrary.ParseValue("RadarModeEnum", "2"));
        Assert.Equal(new TextValue("Hello"), lLibrary.ParseValue("HLAASCIIstring", "Hello"));
        Assert.Throws<XcgfDataException>(() => lLibrary.ParseValue("Count", "two"));
        Assert.Throws<XcgfDataException>(() => lLibrary.ParseValue("RadarModeEnum", "Jamming"));
        Assert.Throws<XcgfDataException>(() => lLibrary.ParseValue("GeoPosition", "0 0 0"));
    }
}
