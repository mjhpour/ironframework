//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace BusinessEntities
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using IronFramework.Common.Data.EntityFramework;
    
    [DataContract]
    public partial class Contact : IEntity
    {
        public Contact()
        {
            this.Employees = new HashSet<Employee>();
        }
    
        [DataMember]
        public int ContactID { get; set; }
        [DataMember]
        public bool NameStyle { get; set; }
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string MiddleName { get; set; }
        [DataMember]
        public string LastName { get; set; }
        [DataMember]
        public string Suffix { get; set; }
        [DataMember]
        public string EmailAddress { get; set; }
        [DataMember]
        public int EmailPromotion { get; set; }
        [DataMember]
        public string Phone { get; set; }
        [DataMember]
        public string PasswordHash { get; set; }
        [DataMember]
        public string PasswordSalt { get; set; }
        [DataMember]
        public string AdditionalContactInfo { get; set; }
        [DataMember]
        public System.Guid rowguid { get; set; }
        [DataMember]
        public System.DateTime ModifiedDate { get; set; }
    
        public virtual ICollection<Employee> Employees { get; set; }
    
        #region IEntity Members
    
        public State State { get; set; }
    
        #endregion
    }
}
